using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Windows.Media.Animation;

namespace Parameter3D
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        ModelVisual3D modVis3D = null;
        ModelVisual3D backgroundModVis3D = null;
        ParameterObjectCollection paramObjCollection;
        ParameterObjectCollection backgroundPObjCollection;
        bool transforming = false;
        Point beginPoint;
        DateTime beginTime;
        ModelVisual3D[] spotLights;
        ModelVisual3D[] directionalLights;
        int selectionIndex = -1;
        EmissiveMaterial highLightMaterial = null;
        string backgroundColorName;

        private enum MouseMode { RotateObject, RotateGroup, DragObject, DragGroup, ScaleObject, ScaleGroup, ScaleXorYorZ, ScaleXandY, ScaleZ, Skew };
        MouseMode mode = MouseMode.RotateGroup;

        bool animating = false;
        Transform3DGroup animationTransformGroup;
        Rotation3DAnimation rotationAnimation;


        public Window1()
        {
            InitializeComponent();
        }

        public int Selection
        {
            get { return selectionIndex; }
            set
            {
                if ( selectionIndex>=0 && (bool)cbxHighlight.IsChecked) RemoveHighlight(selectionIndex);
                selectionIndex = value;
                if (selectionIndex >= 0 && (bool)cbxHighlight.IsChecked) AddHighlight(selectionIndex);
                if (selectionIndex >= 0) LoadParameterObjectDataToUI(paramObjCollection.Children[selectionIndex]);
            }
        }

        public ModelVisual3D ModVis3D
        {
            get { return modVis3D; }
        }

                
        private void AddObject(ParameterObjectData pobj)
        {
            if (pobj == null) return;
            ModelVisual3D newModVis3D = pobj.GetModelVisual3D();
            if (newModVis3D != null)
                if (pobj.Translucent)
                {
                    modVis3D.Children.Add(newModVis3D);
                    paramObjCollection.Children.Add(pobj);
                    Selection = modVis3D.Children.Count - 1;
                }
                else
                {
                    modVis3D.Children.Insert(0, newModVis3D);
                    paramObjCollection.Children.Insert(0, pobj);
                    Selection = 0;
                }
        }

        private void btnAddModel_Click(object sender, RoutedEventArgs e)
        {
            ParameterObjectData paramObjDat;

            try
            {
                paramObjDat = GetParameterObjectDataFromUI();
            }
            catch (Exception except)
            {
                MessageBox.Show("Error parsing input data: " + except.Message);
                return;
            }
            AddObject(paramObjDat);
        }


        private void grid2_MouseMove(object sender, MouseEventArgs e)
        {
            e.Handled = true;
            if (modVis3D.Children.Count == 0) return;
            if (!transforming || e.LeftButton == MouseButtonState.Released)
            {
                transforming = false;
                return;
            }
            Point endPoint = e.GetPosition(grid2);
            DateTime endTime = DateTime.Now;
            Vector3D transformVector = new Vector3D(endPoint.X - beginPoint.X, beginPoint.Y - endPoint.Y, 0);
            if (transformVector.Length < 3) return;

            double mouseSpeed = transformVector.Length / endTime.Subtract(beginTime).TotalMilliseconds;
            beginPoint = endPoint;
            beginTime = endTime;

            Transform3D moveTransform;
            Matrix3D combinedMatrix;
            Matrix3D invGlobalMatrix;
            Matrix3D newObjectMatrix;

            if (mode == MouseMode.RotateGroup || mode == MouseMode.RotateObject)
            {
                Vector3D rotAxis;
                if (beginPoint.Y < (grid2.ActualHeight - 40))
                {
                    rotAxis = Vector3D.CrossProduct(new Vector3D(0, 0, 1), transformVector);
                }
                else
                {
                    if (transformVector.X >0) rotAxis = new Vector3D(0, 0, 1);
                    else rotAxis = new Vector3D(0, 0, -1);
                }
                double rotateAngle = mouseSpeed < 0.1 ? .5 : 2.0;
                moveTransform = new RotateTransform3D(new AxisAngleRotation3D(rotAxis, rotateAngle));
                if (mode == MouseMode.RotateGroup)
                {
                    modVis3D.Transform = new MatrixTransform3D(Matrix3D.Multiply(modVis3D.Transform.Value, moveTransform.Value));
                    return;
                }
                else
                {
                    combinedMatrix = Matrix3D.Multiply(modVis3D.Children[selectionIndex].Transform.Value,modVis3D.Transform.Value);
                    ((RotateTransform3D)moveTransform).CenterX = combinedMatrix.OffsetX;
                    ((RotateTransform3D)moveTransform).CenterY = combinedMatrix.OffsetY;
                    ((RotateTransform3D)moveTransform).CenterZ = combinedMatrix.OffsetZ;
                }
            }
            else if (mode == MouseMode.DragGroup || mode == MouseMode.DragObject)
            {
                Vector3D dragDirection = transformVector;
                dragDirection.Normalize();
                double dragDistance = mouseSpeed < 0.1 ? .01 : .05;
                moveTransform = new TranslateTransform3D(dragDistance*dragDirection.X, dragDistance*dragDirection.Y, 0);
                if (mode == MouseMode.DragGroup)
                {
                    invGlobalMatrix = modVis3D.Transform.Value;
                    invGlobalMatrix.Invert();
                    for (int i = 0; i < modVis3D.Children.Count; i++)
                    {
                        newObjectMatrix = Matrix3D.Multiply(modVis3D.Children[i].Transform.Value,
                                                        Matrix3D.Multiply(modVis3D.Transform.Value,
                                                            Matrix3D.Multiply(moveTransform.Value, invGlobalMatrix)));

                        modVis3D.Children[i].Transform = new MatrixTransform3D(newObjectMatrix);
                        paramObjCollection.Children[i].transformMatrix = newObjectMatrix;
                    }
                    return;
                }
            }
            else if (mode == MouseMode.ScaleGroup || mode == MouseMode.ScaleObject)
            {
                Vector3D scaleVector = (transformVector.Y>0) ? (new Vector3D(1, 1, 1)) * 1.03 : (new Vector3D(1, 1, 1) / 1.03);
                moveTransform = new ScaleTransform3D(scaleVector);
                if (mode == MouseMode.ScaleGroup)
                {
                    modVis3D.Transform = new MatrixTransform3D(Matrix3D.Multiply(moveTransform.Value, modVis3D.Transform.Value));
                    return;
                }
                else //mode must be ScaleObject
                {
                    newObjectMatrix = Matrix3D.Multiply(moveTransform.Value, modVis3D.Children[selectionIndex].Transform.Value);
                    modVis3D.Children[selectionIndex].Transform = new MatrixTransform3D(newObjectMatrix);
                    paramObjCollection.Children[selectionIndex].transformMatrix = newObjectMatrix;
                    return;
                }

            }
            else if (mode == MouseMode.ScaleXorYorZ || mode == MouseMode.ScaleXandY || mode == MouseMode.ScaleZ)
            {
                Vector3D scaleDirection = transformVector;
                scaleDirection.Normalize();
                combinedMatrix = Matrix3D.Multiply(modVis3D.Children[selectionIndex].Transform.Value, modVis3D.Transform.Value);
                double sign = (scaleDirection.X * (endPoint.X - grid2.ActualWidth / 2)
                    + scaleDirection.Y * (grid2.ActualHeight / 2 - endPoint.Y)) > 0 ? 1 : -1;

                Vector3D scaleVector;
                if (mode == MouseMode.ScaleXandY)
                    scaleVector = sign > 0 ? new Vector3D(1.03, 1.03, 1) : new Vector3D(1 / 1.03, 1 / 1.03, 1);
                else if (mode == MouseMode.ScaleZ)
                    scaleVector = sign > 0 ? new Vector3D(1, 1, 1.03) : new Vector3D(1, 1, 1 / 1.03);
                else  // mode must be ScaleXorYorZ
                {
                    Vector3D xPrime = (new Vector3D(1, 0, 0)) * combinedMatrix;
                    xPrime = xPrime - (new Vector3D(0, 0, xPrime.Z));
                    if (xPrime.Length > 0.02) xPrime.Normalize(); else xPrime = new Vector3D(0, 0, 0);
                    Vector3D yPrime = (new Vector3D(0, 1, 0)) * combinedMatrix;
                    yPrime = yPrime - (new Vector3D(0, 0, yPrime.Z));
                    if (yPrime.Length > 0.02) yPrime.Normalize(); else yPrime = new Vector3D(0, 0, 0);
                    Vector3D zPrime = (new Vector3D(0, 0, 1)) * combinedMatrix;
                    zPrime = zPrime - (new Vector3D(0, 0, zPrime.Z));
                    if (zPrime.Length > 0.02) zPrime.Normalize(); else zPrime = new Vector3D(0, 0, 0);
                    double dotX = Math.Abs(Vector3D.DotProduct(scaleDirection, xPrime));
                    double dotY = Math.Abs(Vector3D.DotProduct(scaleDirection, yPrime));
                    double dotZ = Math.Abs(Vector3D.DotProduct(scaleDirection, zPrime));
                    if (dotX > dotY && dotX > dotZ) scaleVector = sign>0? new Vector3D(1.03, 1, 1) : new Vector3D(1/1.03, 1, 1);
                    else if (dotY > dotZ) scaleVector = sign>0? new Vector3D(1, 1.03, 1) : new Vector3D(1, 1/1.03, 1);
                    else scaleVector = sign>0? new Vector3D(1, 1, 1.03) : new Vector3D(1, 1, 1/1.03);
                }
                Matrix3D scaleTransformMatrix = (new ScaleTransform3D(scaleVector)).Value;
                newObjectMatrix = Matrix3D.Multiply(scaleTransformMatrix, modVis3D.Children[selectionIndex].Transform.Value);
                modVis3D.Children[selectionIndex].Transform = new MatrixTransform3D(newObjectMatrix);
                paramObjCollection.Children[selectionIndex].transformMatrix = newObjectMatrix;
                return;
            }
            else // Mode must be Skew
            {
                Matrix3D skewMatrix = Matrix3D.Identity;
                Vector3D skewDirection = transformVector;
                Vector3D skewPosition = new Vector3D(endPoint.X - grid2.ActualWidth / 2, grid2.ActualHeight / 2 - endPoint.Y, 0);
                skewDirection.Normalize();
                combinedMatrix = Matrix3D.Multiply(modVis3D.Children[selectionIndex].Transform.Value, modVis3D.Transform.Value);
                Vector3D xPrime = (new Vector3D(1, 0, 0)) * combinedMatrix;
                xPrime.Normalize();
                Vector3D yPrime = (new Vector3D(0, 1, 0)) * combinedMatrix;
                yPrime.Normalize();
                Vector3D zPrime = (new Vector3D(0, 0, 1)) * combinedMatrix;
                zPrime.Normalize();
                Vector3D zAxis = new Vector3D(0, 0, 1);
                double dotX = Math.Abs(Vector3D.DotProduct(xPrime, zAxis));
                double dotY = Math.Abs(Vector3D.DotProduct(yPrime, zAxis));
                double dotZ = Math.Abs(Vector3D.DotProduct(zPrime, zAxis));
                xPrime = xPrime - xPrime.Z * zAxis;
                yPrime = yPrime - yPrime.Z * zAxis;
                zPrime = zPrime - zPrime.Z * zAxis;
                if (dotX > dotY && dotX > dotZ) //skew in Y-Z plane
                {
                    yPrime.Normalize();
                    zPrime.Normalize();
                    if (Math.Abs(Vector3D.DotProduct(yPrime, skewDirection)) > Math.Abs(Vector3D.DotProduct(zPrime, skewDirection)))
                        skewMatrix.M32 = (Vector3D.DotProduct(skewDirection, yPrime) * Vector3D.DotProduct(skewPosition, zPrime)) > 0 ? .03 : -.03;
                    else skewMatrix.M23 = (Vector3D.DotProduct(skewDirection, zPrime) * Vector3D.DotProduct(skewPosition, yPrime)) > 0 ? .03 : -.03;
                }
                else if (dotY > dotZ) //skew in X-Z plane
                {
                    xPrime.Normalize();
                    zPrime.Normalize();
                    if (Math.Abs(Vector3D.DotProduct(zPrime, skewDirection)) > Math.Abs(Vector3D.DotProduct(xPrime, skewDirection)))
                        skewMatrix.M13 = (Vector3D.DotProduct(skewDirection, zPrime) * Vector3D.DotProduct(skewPosition, xPrime)) > 0 ? .03 : -.03;
                    else skewMatrix.M31 = (Vector3D.DotProduct(skewDirection, xPrime) * Vector3D.DotProduct(skewPosition, zPrime)) > 0 ? .03 : -.03;
                }
                else //skew in X-Y plane
                {
                    xPrime.Normalize();
                    yPrime.Normalize();
                    if (Math.Abs(Vector3D.DotProduct(yPrime, skewDirection)) > Math.Abs(Vector3D.DotProduct(xPrime, skewDirection)))
                        skewMatrix.M12 = (Vector3D.DotProduct(skewDirection, yPrime) * Vector3D.DotProduct(skewPosition, xPrime)) > 0 ? .03 : -.03;
                    else skewMatrix.M21 = (Vector3D.DotProduct(skewDirection, xPrime) * Vector3D.DotProduct(skewPosition, yPrime)) > 0 ? .03 : -.03;
                }
                newObjectMatrix = Matrix3D.Multiply(skewMatrix, modVis3D.Children[selectionIndex].Transform.Value);
                modVis3D.Children[selectionIndex].Transform = new MatrixTransform3D(newObjectMatrix);
                paramObjCollection.Children[selectionIndex].transformMatrix = newObjectMatrix;
                return;
            }
            invGlobalMatrix = modVis3D.Transform.Value;
            invGlobalMatrix.Invert();
            newObjectMatrix = Matrix3D.Multiply(modVis3D.Children[selectionIndex].Transform.Value,
                                            Matrix3D.Multiply(modVis3D.Transform.Value,
                                                Matrix3D.Multiply(moveTransform.Value, invGlobalMatrix)));

            modVis3D.Children[selectionIndex].Transform = new MatrixTransform3D(newObjectMatrix);
            paramObjCollection.Children[selectionIndex].transformMatrix = newObjectMatrix;
        }

        private void grid2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (animating)
            {
                animating = false;
                modVis3D.Transform = new MatrixTransform3D(animationTransformGroup.Value);
                animationTransformGroup = null;
                return;
            }
            if (e.ClickCount == 2)
            {
                bool CtrlKeyDown = (Keyboard.Modifiers & ModifierKeys.Control) > 0;
                Point loc = e.GetPosition(viewPort);
                HitTestResult result = VisualTreeHelper.HitTest(viewPort, loc);
                if (result != null && result.VisualHit is ModelVisual3D)
                {
                    int index = modVis3D.Children.IndexOf((ModelVisual3D)result.VisualHit);
                    if (index >= 0 && index != Selection)
                        if (CtrlKeyDown)
                        {
                            ParameterObjectData pObjData = new ParameterObjectData();
                            ParameterObjectData pOD1 = paramObjCollection.Children[selectionIndex];
                            ParameterObjectData pOD2 = paramObjCollection.Children[index];
                            pObjData.Translucent = pOD1.Translucent;
                            pOD2.Translucent = pOD1.Translucent;
                            pObjData.transformMatrix = pOD1.transformMatrix;
                            pOD1.transformMatrix = Matrix3D.Identity;
                            Matrix3D invertMatrix = pObjData.transformMatrix;
                            invertMatrix.Invert();
                            pOD2.transformMatrix = pOD2.transformMatrix * invertMatrix;
                            pObjData.Children.Add(pOD1);
                            pObjData.Children.Add(pOD2);
                            RemoveObject(selectionIndex);
                            index = paramObjCollection.Children.IndexOf(pOD2);
                            RemoveObject(index);
                            selectionIndex = -1;
                            AddObject(pObjData);
                        }
                        else Selection = index;
                }
            }
            else
            {
                transforming = true;
                beginPoint = e.GetPosition(grid2);
                beginTime = DateTime.Now;
            }
        }

        private void grid2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            transforming = false;
        }

        private void cbxDirectional_Click(object sender, RoutedEventArgs e)
        {
            int index;
            if (sender == cbxDir0) index = 0;
            else if (sender == cbxDir1) index = 1;
            else if (sender == cbxDir2) index = 2;
            else if (sender == cbxDir3) index = 3;
            else if (sender == cbxDir4) index = 4;
            else if (sender == cbxDir5) index = 5;
            else return;

            if ((bool)((CheckBox)sender).IsChecked)
            {
                if (!viewPort.Children.Contains(directionalLights[index])) viewPort.Children.Add(directionalLights[index]);
            }
            else
                if (viewPort.Children.Contains(directionalLights[index])) viewPort.Children.Remove(directionalLights[index]); ;
        }

        private void cbxSpot_Click(object sender, RoutedEventArgs e)
        {
            int index;
            if (sender == cbxSpot0) index = 0;
            else if (sender == cbxSpot1) index = 1;
            else if (sender == cbxSpot2) index = 2;
            else if (sender == cbxSpot3) index = 3;
            else if (sender == cbxSpot4) index = 4;
            else if (sender == cbxSpot5) index = 5;
            else return;

            if ((bool)((CheckBox)sender).IsChecked)
            {
                if (!viewPort.Children.Contains(spotLights[index])) viewPort.Children.Add(spotLights[index]);
            }
            else
                if (viewPort.Children.Contains(spotLights[index])) viewPort.Children.Remove(spotLights[index]); ;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Selection = -1;
            modVis3D = new ModelVisual3D();
            backgroundModVis3D = new ModelVisual3D();
            viewPort.Children.Add(modVis3D);
            viewPort.Children.Add(backgroundModVis3D);
            paramObjCollection = new ParameterObjectCollection();
            backgroundPObjCollection = new ParameterObjectCollection();

            spotLights = new ModelVisual3D[6];
            for (int i = 0; i < 6; i++) spotLights[i] = new ModelVisual3D();
            spotLights[0].Content = new SpotLight(Colors.White, new Point3D(-10, 10, 10), new Vector3D(9, -9, -9), 10, 5);
            spotLights[1].Content = new SpotLight(Colors.White, new Point3D(10, 10, 10), new Vector3D(-9, -9, -9), 8, 4);
            spotLights[2].Content = new SpotLight(Colors.White, new Point3D(-10, 10, 0), new Vector3D(9, -9, 0), 8, 4);
            spotLights[3].Content = new SpotLight(Colors.White, new Point3D(10, 10, 0), new Vector3D(-9, -9, 0), 10, 5);
            spotLights[4].Content = new SpotLight(Colors.White, new Point3D(-10, -10, 10), new Vector3D(9, 9, -9), 8, 4);
            spotLights[5].Content = new SpotLight(Colors.White, new Point3D(10, -10, 10), new Vector3D(-9, 9, -9), 8, 4);

            Color dirColor = Color.FromRgb((byte)128, (byte)128, (byte)128);
            directionalLights = new ModelVisual3D[6];
            for (int i = 0; i < 6; i++) directionalLights[i] = new ModelVisual3D();
            directionalLights[0].Content = new DirectionalLight(dirColor, new Vector3D(9, -9, -9));
            directionalLights[1].Content = new DirectionalLight(dirColor, new Vector3D(-9, -9, -9));
            directionalLights[2].Content = new DirectionalLight(dirColor, new Vector3D(9, -9, 0));
            directionalLights[3].Content = new DirectionalLight(dirColor, new Vector3D(-9, -9, 0));
            directionalLights[4].Content = new DirectionalLight(dirColor, new Vector3D(9, 9, -9));
            directionalLights[5].Content = new DirectionalLight(dirColor, new Vector3D(-9, 9, -9));


            PropertyInfo[] colorsProperties = typeof(Colors).GetProperties();
            for (int i = 0; i < colorsProperties.Length; i++) comboboxColor.Items.Add(colorsProperties[i].Name);
            comboboxColor.SelectedItem = "Gold";

            highLightMaterial = new EmissiveMaterial(new SolidColorBrush(Colors.White));

            backgroundColorName = "Black";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string templateDir = baseDir + "\\Templates";
            templateMenuItem.Tag = templateDir;
            string shapeDir = baseDir + "\\StockPSDFiles";
            shapeMenuItem.Tag = shapeDir;

            string[] cmdLineArgs;
            ParameterObjectCollection cmdLinePObjColl;

            try
            {
                cmdLineArgs = Environment.GetCommandLineArgs();
            }
            catch
            {
                MessageBox.Show("Unable to obtain command line arguments.");
                return;
            }

            if (cmdLineArgs.Length > 1)
            {
                cmdLinePObjColl = ParameterObjectCollection.FromP3DFile(cmdLineArgs[1]);
                if (cmdLinePObjColl != null && cmdLinePObjColl.Children.Count > 0)
                    foreach (ParameterObjectData pobj in cmdLinePObjColl.Children) AddObject(pobj);
            }

        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            if (viewPort != null && viewPort.Children.Contains(modVis3D)) viewPort.Children.Remove(modVis3D);
            modVis3D = new ModelVisual3D();
            paramObjCollection = new ParameterObjectCollection();
            viewPort.Children.Add(modVis3D);
            Selection = -1;
        }

        private void btnRemoveModel_Click(object sender, RoutedEventArgs e)
        {
            RemoveObject(selectionIndex);
        }

        private void RemoveObject(int index)
        {
            if (modVis3D.Children == null || modVis3D.Children.Count == 0 || index < 0 || index > (modVis3D.Children.Count - 1)) return;
            modVis3D.Children.RemoveAt(index);
            paramObjCollection.Children.RemoveAt(index);
            Selection = modVis3D.Children.Count - 1;
        }

        private void cbxHighlight_Click(object sender, RoutedEventArgs e)
        {
            if (selectionIndex < 0) return;
            if ((bool)cbxHighlight.IsChecked) AddHighlight(selectionIndex);
            else RemoveHighlight(selectionIndex);
        }

        private void AddHighlight(int index)
        {
            if (index < 0 || index >= modVis3D.Children.Count) return;  
            ModelVisual3D mVis3D = (ModelVisual3D)modVis3D.Children[index];    
            Model3D mod3D = mVis3D.Content;
            AddHighlightMod3D(mod3D);
        }

        private void AddHighlightMod3D(Model3D mod3D)
        {
            if (mod3D == null) return;
            if (mod3D is Model3DGroup)
            {
                Model3DGroup mod3DGroup = (Model3DGroup)mod3D;
                foreach (Model3D m3D in mod3DGroup.Children)
                {
                    AddHighlightMod3D(m3D);
                }
            }
            else if (mod3D is GeometryModel3D)
            {
                GeometryModel3D gmod3D = (GeometryModel3D)mod3D;
                MaterialGroup matGroup = (MaterialGroup)gmod3D.Material;
                matGroup.Children[3] = highLightMaterial;
            }
        }


        private void RemoveHighlight(int index)
        {
            if (index < 0 || index >= modVis3D.Children.Count) return; 
            ModelVisual3D mVis3D = (ModelVisual3D)modVis3D.Children[index];
            Model3D mod3D = mVis3D.Content;
            RemoveHighlightMod3D(mod3D);
        }

        private void RemoveHighlightMod3D(Model3D mod3D)
        {
            if (mod3D == null) return;
            if (mod3D is Model3DGroup)
            {
                Model3DGroup mod3DGroup = (Model3DGroup)mod3D;
                foreach (Model3D m3D in mod3DGroup.Children)
                {
                    RemoveHighlightMod3D(m3D);
                }
            }
            else if (mod3D is GeometryModel3D)
            {
                GeometryModel3D gmod3D = (GeometryModel3D)mod3D;
                MaterialGroup matGroup = (MaterialGroup)gmod3D.Material;
                matGroup.Children[3] = new DiffuseMaterial(new SolidColorBrush());
            }
        }


        private ParameterObjectData GetParameterObjectDataFromUI()
        {
            ParameterObjectData paramObjDat = null;
            string xFunctionString = tbxXFunction.Text;
            string yFunctionString = tbxYFunction.Text;
            string zFunctionString = tbxZFunction.Text;
            string xPrimeFunctionString = tbxXPrimeFunction.Text;
            string yPrimeFunctionString = tbxYPrimeFunction.Text;
            ExpressionParser boundExpr;
            boundExpr = new ExpressionParser(tbxSMin.Text, null);
            double Smin = boundExpr.runnable(null);
            boundExpr = new ExpressionParser(tbxSMax.Text, null);
            double Smax = boundExpr.runnable(null);
            boundExpr = new ExpressionParser(tbxTMin.Text, null);
            double Tmin = boundExpr.runnable(null);
            boundExpr = new ExpressionParser(tbxTMax.Text, null);
            double Tmax = boundExpr.runnable(null);
            int NumFacetColumns = Int32.Parse(tbxGridSizeS.Text);
            int NumFacetRows = Int32.Parse(tbxGridSizeT.Text);
            bool WrapS = (bool)cbxWrapS.IsChecked;
            bool WrapT = (bool)cbxWrapT.IsChecked;
            string ColString = (string)comboboxColor.SelectedItem;
            if (cbxExtrusion.IsChecked == true) 
                paramObjDat = new ParameterExtrusionObjectData(xFunctionString, yFunctionString, zFunctionString,
                    xPrimeFunctionString, yPrimeFunctionString, Smin, Smax, Tmin, Tmax,
                    NumFacetColumns, NumFacetRows, WrapS, WrapT, ColString);
            else
                paramObjDat = new ParameterObjectData(xFunctionString, yFunctionString, zFunctionString,
                    Smin, Smax, Tmin, Tmax, NumFacetColumns, NumFacetRows, WrapS, WrapT, ColString);
            return paramObjDat;
        }


        private void LoadParameterObjectDataToUI(ParameterObjectData pobj)
        {
            tbxXFunction.Text = pobj.xFunction;
            tbxYFunction.Text = pobj.yFunction;
            tbxZFunction.Text = pobj.zFunction;
            tbxSMin.Text = pobj.Smin.ToString("G5");
            tbxSMax.Text = pobj.Smax.ToString("G5");
            tbxTMin.Text = pobj.Tmin.ToString("G5");
            tbxTMax.Text = pobj.Tmax.ToString("G5");
            tbxGridSizeS.Text = pobj.NumFacetS.ToString();
            tbxGridSizeT.Text = pobj.NumFacetT.ToString();
            cbxWrapS.IsChecked = pobj.WrapS;
            cbxWrapT.IsChecked = pobj.WrapT;
            if (!string.IsNullOrEmpty(pobj.colorString)) comboboxColor.SelectedItem = pobj.colorString;
            if (pobj is ParameterExtrusionObjectData)
            {
                if (cbxExtrusion.IsChecked != true) cbxExtrusion.IsChecked = true;
                tbxXPrimeFunction.Text = ((ParameterExtrusionObjectData)pobj).xPrimeFunction;
                tbxYPrimeFunction.Text = ((ParameterExtrusionObjectData)pobj).yPrimeFunction;

            }
            else
            {
                if (cbxExtrusion.IsChecked != false) cbxExtrusion.IsChecked = false;
            }
        }

        private void ModeSelectItemHandler(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            if (item == rotateObjectMenuItem || item == rotateObjectCmenuItem) mode = MouseMode.RotateObject;
            else if (item == rotateGroupMenuItem || item == rotateGroupCmenuItem) mode = MouseMode.RotateGroup;
            else if (item == dragObjectMenuItem || item == dragObjectCmenuItem) mode = MouseMode.DragObject;
            else if (item == dragGroupMenuItem || item == dragGroupCmenuItem) mode = MouseMode.DragGroup;
            else if (item == scaleObjectMenuItem || item == scaleObjectCmenuItem) mode = MouseMode.ScaleObject;
            else if (item == scaleXorYorZMenuItem || item == scaleXorYorZCmenuItem) mode = MouseMode.ScaleXorYorZ;
            else if (item == scaleXandYMenuItem || item == scaleXandYCmenuItem) mode = MouseMode.ScaleXandY;
            else if (item == scaleZMenuItem || item == scaleZCmenuItem) mode = MouseMode.ScaleZ;
            else if (item == scaleGroupMenuItem || item == scaleGroupCmenuItem) mode = MouseMode.ScaleGroup;
            else if (item == skewObjectMenuItem || item == skewObjectCmenuItem) mode = MouseMode.Skew;
            else return;
            rotateObjectMenuItem.IsChecked = rotateObjectCmenuItem.IsChecked = mode == MouseMode.RotateObject;
            rotateGroupMenuItem.IsChecked = rotateGroupCmenuItem.IsChecked = mode == MouseMode.RotateGroup;
            dragObjectMenuItem.IsChecked = dragObjectCmenuItem.IsChecked = mode == MouseMode.DragObject;
            dragGroupMenuItem.IsChecked = dragGroupCmenuItem.IsChecked = mode == MouseMode.DragGroup;
            scaleObjectMenuItem.IsChecked = scaleObjectCmenuItem.IsChecked = mode == MouseMode.ScaleObject;
            scaleXorYorZMenuItem.IsChecked = scaleXorYorZCmenuItem.IsChecked = mode == MouseMode.ScaleXorYorZ;
            scaleXandYMenuItem.IsChecked = scaleXandYCmenuItem.IsChecked = mode == MouseMode.ScaleXandY;
            scaleZMenuItem.IsChecked = scaleZCmenuItem.IsChecked = mode == MouseMode.ScaleZ;
            scaleGroupMenuItem.IsChecked = scaleGroupCmenuItem.IsChecked = mode == MouseMode.ScaleGroup;
            skewObjectMenuItem.IsChecked = skewObjectCmenuItem.IsChecked = mode == MouseMode.Skew;
            scaleMenuItem.IsChecked = scaleCmenuItem.IsChecked = mode == MouseMode.ScaleObject || mode == MouseMode.ScaleGroup 
                || mode == MouseMode.ScaleXorYorZ || mode == MouseMode.ScaleXandY || mode == MouseMode.ScaleZ;
            dragMenuItem.IsChecked = dragCmenuItem.IsChecked = mode == MouseMode.DragGroup || mode == MouseMode.DragObject;
        }


        private void saveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (paramObjCollection.Children.Count == 0) return;
            retrieveAllMenuItem_Click(null, null);
            BinaryFormatter bf = null;
            FileStream outfile = null;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Parameter 3D files (.p3d)|*.p3d";
            dlg.DefaultExt = ".p3d";
            bool? result = dlg.ShowDialog();
            try
            {
                if (result == true)
                {
                    bf = new BinaryFormatter();
                    outfile = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
                    bf.Serialize(outfile, paramObjCollection);
                    outfile.Close();
                }
            }
            catch (Exception except)
            {
                MessageBox.Show("Save File exception: " + except.Message);
                if (outfile != null) outfile.Close();
            }
        }

        private void openMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ParameterObjectCollection pObjColl = null;
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Parameter 3D files (.p3d)|*.p3d";
            dlg.DefaultExt = ".p3d";
            bool? result = dlg.ShowDialog();
            pObjColl = ParameterObjectCollection.FromP3DFile(dlg.FileName);
            if (pObjColl != null && pObjColl.Children.Count > 0) foreach (ParameterObjectData pobj in pObjColl.Children) AddObject(pobj);

        }

        private void splitObjectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (selectionIndex >= 0 && (((ModelVisual3D)modVis3D.Children[selectionIndex]).Content is Model3DGroup))
            {
                int tempIndex = selectionIndex;
                Model3DGroup m3DGroup = (Model3DGroup)((ModelVisual3D)modVis3D.Children[selectionIndex]).Content;
                ParameterObjectData pobj = paramObjCollection.Children[selectionIndex];
                RemoveObject(selectionIndex);
                for (int i = 0; i < m3DGroup.Children.Count; i++)
                {
                    pobj.Children[i].transformMatrix = Matrix3D.Multiply(pobj.Children[i].transformMatrix, pobj.transformMatrix); //changed code
                    AddObject(pobj.Children[i]);
                }
            }
        }

        private void resetObjectTransformMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (selectionIndex < 0) return;
            modVis3D.Children[selectionIndex].Transform = Transform3D.Identity;
            paramObjCollection.Children[selectionIndex].transformMatrix = Matrix3D.Identity;
        }

        private void resetGroupTransformMenuItem_Click(object sender, RoutedEventArgs e)
        {
            modVis3D.Transform = Transform3D.Identity;
        }

        private void displayTransformMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (selectionIndex < 0) return;
            Matrix3D m = paramObjCollection.Children[selectionIndex].transformMatrix;
            MessageBox.Show("Current Object Transform Matrix:"
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.M11, m.M12, m.M13)
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.M21, m.M22, m.M23)
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.M31, m.M32, m.M33)
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.OffsetX, m.OffsetY, m.OffsetZ));
        }


        private void SetModel3DColor(Model3D mod3D, string colStr)
        {
            if (String.IsNullOrEmpty(colStr)) return;
            if (mod3D is Model3DGroup)
                foreach (Model3D m3D in ((Model3DGroup)mod3D).Children) SetModel3DColor(m3D, colStr);
            else if (mod3D is GeometryModel3D)
            {
                PropertyInfo prop = typeof(Colors).GetProperty(colStr);
                if (prop == null) return;
                Color color = (Color)prop.GetValue(null,null);
                DiffuseMaterial material =(DiffuseMaterial)((MaterialGroup)((GeometryModel3D)mod3D).Material).Children[0];
                Brush br = material.Brush;
                ((SolidColorBrush)br).Color = color;
            }
        }

        private void SetModel3DBackColor(Model3D mod3D, string backColStr)
        {
            if (String.IsNullOrEmpty(backColStr)) return;
            if (mod3D is Model3DGroup)
                foreach (Model3D m3D in ((Model3DGroup)mod3D).Children) SetModel3DBackColor(m3D, backColStr);
            else if (mod3D is GeometryModel3D)
            {
                PropertyInfo prop = typeof(Colors).GetProperty(backColStr);
                if (prop == null) return;
                Color backColor = (Color)prop.GetValue(null, null);
                DiffuseMaterial backMaterial =(DiffuseMaterial) ((MaterialGroup)((GeometryModel3D)mod3D).BackMaterial).Children[0];
                Brush backBr = backMaterial.Brush;
                ((SolidColorBrush)backBr).Color = backColor;
            }
        }

        private void SetParameterObjectDataColor(ParameterObjectData pobj, string colStr)
        {
            if (String.IsNullOrEmpty(colStr)) return;
            if (pobj.Children.Count > 0)
                foreach (ParameterObjectData pod in pobj.Children) SetParameterObjectDataColor(pod, colStr);
            else
                pobj.colorString = colStr;
        }

        

        private string GetParameterObjectDataColor(ParameterObjectData pobj)
        {
            if (pobj.Children.Count > 0) return GetParameterObjectDataColor(pobj.Children[0]);
            else return pobj.colorString;
        }

        private void changeColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (selectionIndex < 0) return;
            SetModel3DColor(((ModelVisual3D)modVis3D.Children[selectionIndex]).Content, (string)comboboxColor.SelectedItem);
            paramObjCollection.Children[selectionIndex].ChangeColor((string)comboboxColor.SelectedItem);
        }

        private void changeBackColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (selectionIndex < 0) return;
            SetModel3DBackColor(((ModelVisual3D)modVis3D.Children[selectionIndex]).Content, (string)comboboxColor.SelectedItem);
            paramObjCollection.Children[selectionIndex].ChangeBackColor((string)comboboxColor.SelectedItem);

        }

        private void changeBackgroundColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            String backgroundColStr = (String)comboboxColor.SelectedItem;
            if (String.IsNullOrEmpty(backgroundColStr)) return;
            PropertyInfo prop = typeof(Colors).GetProperty(backgroundColStr);
            if (prop == null) return;
            Color color = (Color)prop.GetValue(null, null);
            grid2.Background = new SolidColorBrush(color);
            backgroundColorName = backgroundColStr;
        }

        private void CameraEventHandler(object sender, RoutedEventArgs e)
        {
            if (sldZoom == null || sldDistance == null || viewPort == null ) return;
            if (btnOrthographicCamera.IsChecked == true)
            {
                sldDistance.IsEnabled = lblDistance.IsEnabled = false;
                double cameraWidth = 12 - sldZoom.Value;
                viewPort.Camera = new OrthographicCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), cameraWidth);
            }
            else if (btnPerspectiveCamera.IsChecked == true)
            {
                sldDistance.IsEnabled = lblDistance.IsEnabled = true;
                double angle = 30 - 2 * sldZoom.Value;
                double distance = sldDistance.Value;
                viewPort.Camera = new PerspectiveCamera(new Point3D(0, 0, distance), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), angle);
            }
            else return;
        }


        private void saveImageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FileStream outfile = null;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "JPEG file (.jpg)|*.jpg";
            dlg.DefaultExt = ".jpg";
            bool? result = dlg.ShowDialog();
            try
            {
                if (result == true)
                {
                    int height = (int)this.grid2.ActualHeight;
                    int width = (int)this.grid2.ActualWidth;
                    DrawingVisual dv = new DrawingVisual();
                    using (DrawingContext ctx = dv.RenderOpen())
                    {
                        VisualBrush br = new VisualBrush();
                        br.AutoLayoutContent = true;
                        br.Visual = grid2;
                        ctx.DrawRectangle(br, null, new Rect(0, 0, width, height));
                    }
                    outfile = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
                    RenderTargetBitmap bmp = new RenderTargetBitmap(width,
                        height, 1 / 96, 1 / 96, PixelFormats.Pbgra32);
                    bmp.Render(dv);
                    BitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                    encoder.Save(outfile);
                    outfile.Close();
                }
            }
            catch (Exception except)
            {
                MessageBox.Show("Save File exception: " + except.Message);
                if (outfile != null) outfile.Close();
            }

        }


        private void closeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void cbxExtrusion_Checked(object sender, RoutedEventArgs e)
        {
            lblX.Content = "x(s) =";
            lblY.Content = "y(s) =";
            lblZ.Content = "z(s) =";
            lblXPrime.IsEnabled = true;
            lblYPrime.IsEnabled = true;
            tbxXPrimeFunction.IsEnabled = true;
            tbxYPrimeFunction.IsEnabled = true;
        }

        private void cbxExtrusion_Unchecked(object sender, RoutedEventArgs e)
        {
            lblX.Content = "x(s,t) =";
            lblY.Content = "y(s,t) =";
            lblZ.Content = "z(s,t) =";
            lblXPrime.IsEnabled = false;
            lblYPrime.IsEnabled = false;
            tbxXPrimeFunction.Text = "";
            tbxXPrimeFunction.IsEnabled = false;
            tbxYPrimeFunction.Text = "";
            tbxYPrimeFunction.IsEnabled = false;
        }

        private void helpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            HelpDialog helpDlg = new HelpDialog();
            helpDlg.ShowDialog();
        }

        private void createTemplateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CreateTemplatedialog createTemplateDlg = new CreateTemplatedialog();
            createTemplateDlg.ShowDialog();
        }

        private void openTemplateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Parameter 3D Template files (.p3t)|*.p3t";
            dlg.DefaultExt = ".p3t";
            bool? result = dlg.ShowDialog();
            if (result != true)
            {
                MessageBox.Show("No template file was opened.");
                return;
            }

            OpenTemplate(dlg.FileName);

        }

        public void OpenTemplate(string fname)
        {
            BinaryFormatter bf = null;
            FileStream infile = null;
            ParameterObjectTemplate pObjTemplate = null;

            try
            {
                bf = new BinaryFormatter();
                infile = new FileStream(fname, FileMode.Open, FileAccess.Read);
                pObjTemplate = (ParameterObjectTemplate)bf.Deserialize(infile);
            }
            catch (Exception except)
            {
                MessageBox.Show("Template File Read or Deserialization exception: " + except.Message);
                return;
            }
            finally
            {
                if (infile != null) infile.Close();
            }

            OpenTemplateDialog openTemplateDlg = new OpenTemplateDialog(pObjTemplate);
            bool? result = openTemplateDlg.ShowDialog();
            if (result != true)
            {
                MessageBox.Show("No ParameterObjectData was created.");
                return;
            }

            ParameterObjectData pObjData = (ParameterObjectData)(openTemplateDlg.Tag);
            AddObject(pObjData);
        }

        public void templateMenuItems_Click(object sender, RoutedEventArgs e)
        {
                string templateFileName = (string)((MenuItem)sender).Tag;
                OpenTemplate(templateFileName);
        }

        public void shapeMenuItems_Click(object sender, RoutedEventArgs e)
        {
            string shapeFileName = (string)((MenuItem)sender).Tag;
            ParameterObjectCollection pObjColl = ParameterObjectCollection.FromP3DFile(shapeFileName);
            if (pObjColl == null) return;
            for (int i = 0; i < pObjColl.Children.Count; i++)
            {
                pObjColl.Children[i].ChangeColor((string)comboboxColor.SelectedItem);
                AddObject(pObjColl.Children[i]);
            }
        }

        private void combineObjectsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (modVis3D.Children == null || modVis3D.Children.Count == 0) return;
            ParameterObjectData pObjData = new ParameterObjectData();
            pObjData.transformMatrix = paramObjCollection.Children[selectionIndex].transformMatrix;
            pObjData.Translucent = paramObjCollection.Children[selectionIndex].Translucent;
            Matrix3D invertMatrix = pObjData.transformMatrix;
            invertMatrix.Invert();
            for (int i = 0; i < modVis3D.Children.Count; i++)
            {
                pObjData.Children.Add(paramObjCollection.Children[i]);
                pObjData.Children[i].transformMatrix = Matrix3D.Multiply(pObjData.Children[i].transformMatrix, invertMatrix);
            }
            modVis3D.Children.Clear();
            paramObjCollection.Children.Clear();
            selectionIndex = -1;
            AddObject(pObjData);
        }

        private void templateMenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            templateMenuItem.Items.Clear();
            try
            {
                FillDirectoryMenuItem(templateMenuItem, "*.p3t", templateMenuItems_Click);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error populating the template menu: " + ex.ToString());
            }
        }

        private void FillDirectoryMenuItem(MenuItem mi, string fmt, RoutedEventHandler eventHandler)
        {
            string dirPath = (string)mi.Tag;
            string[] directories = Directory.GetDirectories(dirPath);
            string[] files = Directory.GetFiles(dirPath, fmt);
            if (files != null && files.Length > 0) for (int i = 0; i < files.Length; i++)
                {
                    MenuItem nextMenuItem = new MenuItem();
                    nextMenuItem.Tag = files[i];
                    nextMenuItem.Header = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                    nextMenuItem.Click += eventHandler;
                    mi.Items.Add(nextMenuItem);
                }
            if (directories != null && directories.Length > 0) for (int i = 0; i < directories.Length; i++)
                {
                    MenuItem nextMenuItem = new MenuItem();
                    nextMenuItem.Tag = directories[i];
                    nextMenuItem.Header = System.IO.Path.GetFileNameWithoutExtension(directories[i]);
                    FillDirectoryMenuItem(nextMenuItem, fmt, eventHandler);
                    mi.Items.Add(nextMenuItem);
                }
        }

        private void aboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Parameter3D v1.0.0  by James Kenney");
        }

        private void shapeTBItem_Click(object sender, RoutedEventArgs e)
        {
            
            Button b = (Button)sender;
            BinaryFormatter bf = null;
            FileStream infile = null;
            ParameterObjectCollection pObjColl = null;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string filename = baseDir + "\\StockPSDFiles\\" + (string)b.Tag;
            string colStr = (string)comboboxColor.SelectedItem;
            try
            {

                bf = new BinaryFormatter();
                infile = new FileStream(filename, FileMode.Open, FileAccess.Read);
                pObjColl = (ParameterObjectCollection)bf.Deserialize(infile);
            }
            catch (Exception except)
            {
                MessageBox.Show("File Read or Deserialization exception: " + except.Message);
                return;
            }
            finally
            {
                if (infile != null) infile.Close();
            }
            if (pObjColl.Children.Count > 0) foreach (ParameterObjectData pobj in pObjColl.Children) AddObject(pobj);
            comboboxColor.SelectedItem = colStr;
            changeColorMenuItem_Click(null, null);

        }

        private void animateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!animating)
            {
                animating = true;
                animationTransformGroup = new Transform3DGroup();
                animationTransformGroup.Children.Add(modVis3D.Transform);
                AxisAngleRotation3D beginRotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
                AxisAngleRotation3D endRotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 60);
                animationTransformGroup.Children.Add(new RotateTransform3D(beginRotation));
                rotationAnimation = new Rotation3DAnimation(beginRotation, endRotation, new Duration(TimeSpan.Parse("0:0:1")));
                rotationAnimation.RepeatBehavior = RepeatBehavior.Forever;
                rotationAnimation.IsCumulative = true;
                modVis3D.Transform = animationTransformGroup;
                ((Transform3DGroup)modVis3D.Transform).Children[1].BeginAnimation(RotateTransform3D.RotationProperty, rotationAnimation);
            }
            else
            {
                animating = false;
                modVis3D.Transform = new MatrixTransform3D(animationTransformGroup.Value);
                animationTransformGroup = null;
            }

        }

        private void addImageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (paramObjCollection.Children.Count == 0) return;
            if (selectionIndex < 0) return;
            if (paramObjCollection.Children[selectionIndex].Children != null && paramObjCollection.Children[selectionIndex].Children.Count > 0)
            {
                MessageBox.Show("Cannot add image to compound object");
                return;
            }
            if (paramObjCollection.Children[selectionIndex].imageSource != null)
            {
                MessageBox.Show("Object already has image.  Must remove existing image before adding new one.");
                return;
            }
            AddImageDialog dlg = new AddImageDialog(paramObjCollection.Children[selectionIndex], (GeometryModel3D)((ModelVisual3D)modVis3D.Children[selectionIndex]).Content);
            bool? result = dlg.ShowDialog();
        }

        private void removeImageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (modVis3D.Children == null || modVis3D.Children.Count == 0 || selectionIndex < 0) return;
            if (((ModelVisual3D)modVis3D.Children[selectionIndex]).Content is Model3DGroup) return;
            GeometryModel3D geomMod3D = (GeometryModel3D)((ModelVisual3D)modVis3D.Children[selectionIndex]).Content;
            ((MaterialGroup)geomMod3D.Material).Children[1] = new DiffuseMaterial(new SolidColorBrush());
            paramObjCollection.Children[selectionIndex].RemoveImage();
        }

        private void transformImageEventHandler(object sender, RoutedEventArgs e)
        {
            Matrix m;
            if (modVis3D.Children == null || modVis3D.Children.Count == 0 || selectionIndex < 0) return;
            if (((ModelVisual3D)modVis3D.Children[selectionIndex]).Content is Model3DGroup) return;
            if (paramObjCollection.Children[selectionIndex].imageSource == null) return;
            GeometryModel3D geomMod3D = (GeometryModel3D)((ModelVisual3D)modVis3D.Children[selectionIndex]).Content;
            if (sender == flipSMenuItem) m = new Matrix(-1, 0, 0, 1, 0, 0);
            else if (sender == flipTMenuItem) m = new Matrix(1, 0, 0, -1, 0, 0);
            else if (sender == rotateImageMenuItem) m = (new RotateTransform(90)).Value;
            else return;
            paramObjCollection.Children[selectionIndex].TransformImage(geomMod3D, m);
        }

        private void toggleTilingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (modVis3D.Children == null || modVis3D.Children.Count == 0 || selectionIndex < 0) return;
            if (((ModelVisual3D)modVis3D.Children[selectionIndex]).Content is Model3DGroup) return;
            if (paramObjCollection.Children[selectionIndex].imageSource == null) return;
            GeometryModel3D geomMod3D = (GeometryModel3D)((ModelVisual3D)modVis3D.Children[selectionIndex]).Content;
            if (paramObjCollection.Children[selectionIndex].imageTiled) paramObjCollection.Children[selectionIndex].UnTileImage(geomMod3D);
            else paramObjCollection.Children[selectionIndex].TileImage(geomMod3D);
        }

        private void moveToBackgroundMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (modVis3D.Children == null || modVis3D.Children.Count == 0 || selectionIndex < 0) return;
            ModelVisual3D mVis3D = (ModelVisual3D)modVis3D.Children[selectionIndex];
            ParameterObjectData pObjData = paramObjCollection.Children[selectionIndex];
            RemoveObject(selectionIndex);
            mVis3D.Transform = new MatrixTransform3D(mVis3D.Transform.Value * modVis3D.Transform.Value);
            pObjData.transformMatrix = pObjData.transformMatrix * modVis3D.Transform.Value;
            backgroundPObjCollection.Children.Add(pObjData);
            backgroundModVis3D.Children.Add(mVis3D);
        }

        private void retrieveAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (backgroundModVis3D.Children == null || backgroundModVis3D.Children.Count == 0) return;
            Matrix3D mInverse = modVis3D.Transform.Value;
            mInverse.Invert();
            for (int i = backgroundModVis3D.Children.Count - 1; i >= 0; i-- )
            {
                ParameterObjectData pOD = backgroundPObjCollection.Children[i];
                backgroundModVis3D.Children.RemoveAt(i);
                backgroundPObjCollection.Children.RemoveAt(i);
                pOD.transformMatrix = pOD.transformMatrix * mInverse;
                AddObject(pOD);
            }
        }

        private void toggleTranslucencyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (modVis3D.Children == null || modVis3D.Children.Count == 0 || selectionIndex < 0) return;
            Model3D m3D = ((ModelVisual3D)modVis3D.Children[selectionIndex]).Content;
            ParameterObjectData pODTemp;
            ModelVisual3D mVis3DTemp = (ModelVisual3D)modVis3D.Children[selectionIndex];
            if (paramObjCollection.Children[selectionIndex].Translucent)
            {
                pODTemp = paramObjCollection.Children[selectionIndex];
                pODTemp.Translucent = false;
                SetModel3DOpacity(m3D, false);
                paramObjCollection.Children.RemoveAt(selectionIndex);
                paramObjCollection.Children.Insert(0, pODTemp);
                modVis3D.Children.RemoveAt(selectionIndex);
                modVis3D.Children.Insert(0, mVis3DTemp);
                Selection = 0;
            }
            else
            {
                pODTemp = paramObjCollection.Children[selectionIndex];
                pODTemp.Translucent = true;
                SetModel3DOpacity(m3D, true);
                paramObjCollection.Children.RemoveAt(selectionIndex);
                paramObjCollection.Children.Insert(paramObjCollection.Children.Count, pODTemp);
                modVis3D.Children.RemoveAt(selectionIndex);
                modVis3D.Children.Insert(modVis3D.Children.Count, mVis3DTemp);
                Selection = modVis3D.Children.Count - 1;
            }
        }

        private void SetModel3DOpacity(Model3D m3D, bool translucent)
        {
            if (m3D is Model3DGroup) foreach (Model3D m in ((Model3DGroup)m3D).Children) SetModel3DOpacity(m, translucent);
            else
            {
                GeometryModel3D gm3D = (GeometryModel3D)m3D;
                DiffuseMaterial material =(DiffuseMaterial) ((MaterialGroup)gm3D.Material).Children[0];
                SpecularMaterial materialSpecular = (SpecularMaterial)((MaterialGroup)gm3D.Material).Children[2];
                DiffuseMaterial backMaterial =(DiffuseMaterial) ((MaterialGroup)gm3D.BackMaterial).Children[0];
                SpecularMaterial backMaterialSpecular = (SpecularMaterial)((MaterialGroup)gm3D.BackMaterial).Children[1];
                Brush br = material.Brush;
                Brush backBr = backMaterial.Brush;
                if (translucent)
                {
                    br.Opacity = 0.3;
                    backBr.Opacity = 0.3;
                }
                else
                {
                    br.Opacity = 1.0; 
                    backBr.Opacity = 1.0;
                }
                ((MaterialGroup)gm3D.Material).Children[0] = material;
                ((MaterialGroup)gm3D.BackMaterial).Children[0] = backMaterial;
            }
        }

        private void ShapeMenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            shapeMenuItem.Items.Clear();
            try
            {
                FillDirectoryMenuItem(shapeMenuItem, "*.p3d", shapeMenuItems_Click);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error populating the template menu: " + ex.ToString());
            }

        }

    }
}
