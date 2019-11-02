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
        bool transforming = false;
        Point beginPoint;
        DateTime beginTime;
        ModelVisual3D[] spotLights;
        ModelVisual3D[] directionalLights;
        string backgroundColorName;

        private enum MouseMode { RotateObject, RotateGroup, DragObject, DragGroup, ScaleObject, ScaleGroup, ScaleXorYorZ, ScaleXandY, ScaleZ, Skew };
        MouseMode mode = MouseMode.RotateGroup;

        bool animating = false;
        Transform3DGroup animationTransformGroup;
        Rotation3DAnimation rotationAnimation;

        List<ParamModVis3D> selectionList = new List<ParamModVis3D>();


        public Window1()
        {
            InitializeComponent();
        }

        ParamModVis3D Selection
        {
            get { if (selectionList.Count == 0) return null; else return selectionList[0]; }
            set
            {
                if (value != null)
                {
                    selectionList.Clear();
                    selectionList.Add(value);
                    if (rbnNoHighlight.IsChecked == false) UpdateHighlights();
                    LoadParamModVis3DToUI(selectionList[0]);
                }
            }
        }

                
        private void AddObject(ParamModVis3D pmv3D)
        {
            if (pmv3D == null || pmv3D.Content == null) return;
            if (pmv3D.Opaque) modVis3D.Children.Insert(0, pmv3D);
            else modVis3D.Children.Add(pmv3D);
            Selection = pmv3D;
        }


        private void btnAddModel_Click(object sender, RoutedEventArgs e)
        {
            ParamModVis3D pmv3D = null;

            try
            {
                pmv3D = GetParamModVis3DFromUI();
                if (pmv3D != null) AddObject(pmv3D);
                else MessageBox.Show("Null ParamModVis3D object generated.  Check input data.");
            }
            catch (Exception except)
            {
                MessageBox.Show("Add Model Exception: " + except.Message);
                return;
            }
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
                    modVis3D.Transform = new MatrixTransform3D( modVis3D.Transform.Value * moveTransform.Value);
                    return;
                }
                else
                {
                    combinedMatrix = Selection.TransformMatrix * modVis3D.Transform.Value;
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
                    foreach (ParamModVis3D pmv3D in modVis3D.Children)
                    {
                        pmv3D.TransformMatrix = pmv3D.TransformMatrix * modVis3D.Transform.Value * moveTransform.Value * invGlobalMatrix;
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
                    modVis3D.Transform = new MatrixTransform3D( moveTransform.Value * modVis3D.Transform.Value);
                    return;
                }
                else //mode must be ScaleObject
                {
                    Selection.TransformMatrix = moveTransform.Value * Selection.TransformMatrix;
                    return;
                }

            }
            else if (mode == MouseMode.ScaleXorYorZ || mode == MouseMode.ScaleXandY || mode == MouseMode.ScaleZ)
            {
                Vector3D scaleDirection = transformVector;
                scaleDirection.Normalize();
                combinedMatrix = Selection.TransformMatrix * modVis3D.Transform.Value;
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
                Selection.TransformMatrix = scaleTransformMatrix * Selection.TransformMatrix;
                return;
            }
            else // Mode must be Skew
            {
                Matrix3D skewMatrix = Matrix3D.Identity;
                Vector3D skewDirection = transformVector;
                Vector3D skewPosition = new Vector3D(endPoint.X - grid2.ActualWidth / 2, grid2.ActualHeight / 2 - endPoint.Y, 0);
                skewDirection.Normalize();
                combinedMatrix = Selection.TransformMatrix * modVis3D.Transform.Value;
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
                Selection.TransformMatrix = skewMatrix * Selection.TransformMatrix;
                return;
            }
            invGlobalMatrix = modVis3D.Transform.Value;
            invGlobalMatrix.Invert();
            Selection.TransformMatrix = Selection.TransformMatrix * modVis3D.Transform.Value * moveTransform.Value * invGlobalMatrix;
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
                    ParamModVis3D pmv3D;
                    int index = modVis3D.Children.IndexOf((ModelVisual3D)result.VisualHit);
                    if (index<0) return;
                    pmv3D = (ParamModVis3D)modVis3D.Children[index];
                    if (CtrlKeyDown)
                    {
                        if (Selection == null) return;
                        if (selectionList.Contains(pmv3D)) return;
                        selectionList.Add(pmv3D);
                        if (rbnNoHighlight.IsChecked == false) UpdateHighlights();
                    }
                    else Selection = pmv3D;
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
            Selection = null;

            modVis3D = new ModelVisual3D();
            backgroundModVis3D = new ModelVisual3D();
            viewPort.Children.Add(modVis3D);
            viewPort.Children.Add(backgroundModVis3D);

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

            backgroundColorName = "Black";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string templateDir = baseDir + "\\Templates";
            templateMenuItem.Tag = templateDir;
            string shapeDir = baseDir + "\\StockPSDFiles";
            shapeMenuItem.Tag = shapeDir;

            string[] cmdLineArgs;

            try
            {
                cmdLineArgs = Environment.GetCommandLineArgs();
            }
            catch
            {
                MessageBox.Show("Unable to obtain command line arguments.");
                return;
            }

            if (cmdLineArgs.Length > 1) OpenP3DFile(cmdLineArgs[1]);

        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            modVis3D.Children.Clear();
            modVis3D.Transform = Transform3D.Identity;
            Selection = null;
        }

        private void btnRemoveModel_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            modVis3D.Children.Remove(Selection);
            if (modVis3D.Children.Count == 0) Selection = null; else Selection = (ParamModVis3D)modVis3D.Children[modVis3D.Children.Count-1];
        }

        private ParamModVis3D GetParamModVis3DFromUI()
        {
            ParamData pd;
            try
            {
                string xFunctionString = tbxXFunction.Text;
                string yFunctionString = tbxYFunction.Text;
                string zFunctionString = tbxZFunction.Text;
                string xPrimeFunctionString = tbxXPrimeFunction.Text;
                string yPrimeFunctionString = tbxYPrimeFunction.Text;
                string Smin = tbxSMin.Text;
                string Smax = tbxSMax.Text;
                string Tmin = tbxTMin.Text;
                string Tmax = tbxTMax.Text;
                int nFacS = Int32.Parse(tbxGridSizeS.Text);
                int nFacT = Int32.Parse(tbxGridSizeT.Text);
                string ColString = (string)comboboxColor.SelectedItem;
                string name = tbxName.Text;
                if (cbxExtrusion.IsChecked == true)
                    pd = new ParamExtrusionData(xFunctionString, yFunctionString, zFunctionString,
                        xPrimeFunctionString, yPrimeFunctionString, Smin, Smax, Tmin, Tmax,
                        nFacS, nFacT, ColString, name);
                else
                    pd = new ParamData(xFunctionString, yFunctionString, zFunctionString,
                        Smin, Smax, Tmin, Tmax, nFacS, nFacT, ColString, name);
                return new ParamModVis3D(pd);
            }
            catch { return null; }
        }


        private void LoadParamModVis3DToUI(ParamModVis3D pmv3D)
        {
            if (pmv3D == null || pmv3D.ParamDataContent == null) return;
            tbxName.Text = pmv3D.Name;
            ParamData pd = pmv3D.ParamDataContent;
            if (!string.IsNullOrEmpty(pd.ColorString)) comboboxColor.SelectedItem = pd.ColorString;
            if (pd.Children.Count == 0)
            {
                tbxXFunction.Text = pd.XExpr;
                tbxYFunction.Text = pd.YExpr;
                tbxZFunction.Text = pd.ZExpr;
                tbxSMin.Text = pd.SMin;
                tbxSMax.Text = pd.SMax;
                tbxTMin.Text = pd.TMin;
                tbxTMax.Text = pd.TMax;
                tbxGridSizeS.Text = pd.NumFacetS.ToString();
                tbxGridSizeT.Text = pd.NumFacetT.ToString();
                if (!string.IsNullOrEmpty(pd.ColorString)) comboboxColor.SelectedItem = pd.ColorString;
                if (pd is ParamExtrusionData)
                {
                    if (cbxExtrusion.IsChecked != true) cbxExtrusion.IsChecked = true;
                    tbxXPrimeFunction.Text = ((ParamExtrusionData)pd).XPrimeExpr;
                    tbxYPrimeFunction.Text = ((ParamExtrusionData)pd).YPrimeExpr;
                }
                else
                {
                    if (cbxExtrusion.IsChecked != false) cbxExtrusion.IsChecked = false;
                    tbxXPrimeFunction.Clear();
                    tbxYPrimeFunction.Clear();
                }
            }
            else
            {
                tbxXFunction.Clear(); tbxYFunction.Clear(); tbxZFunction.Clear(); tbxSMin.Clear(); tbxSMax.Clear();
                tbxTMin.Clear(); tbxTMax.Clear(); tbxGridSizeS.Clear(); tbxGridSizeT.Clear(); tbxXPrimeFunction.Clear();
                tbxYPrimeFunction.Clear();
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
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Parameter 3D files (.p3d)|*.p3d";
            dlg.DefaultExt = ".p3d";
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                List<ParamModVis3D> list = new List<ParamModVis3D>();
                foreach (ModelVisual3D m in modVis3D.Children) list.Add(m as ParamModVis3D);
                bool success = ParamModVis3D.Save(list, dlg.FileName);
                if (!success) MessageBox.Show("File Save was unsuccessful");
            }
        }

        private void openMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Parameter 3D files (.p3d)|*.p3d";
            dlg.DefaultExt = ".p3d";
            bool? result = dlg.ShowDialog();
            if (result == true) OpenP3DFile(dlg.FileName);
        }

        void OpenP3DFile(string name)
        {
            try
            {
                List<ParamModVis3D> list;
                list = ParamModVis3D.FromP3DFile(name);
                if (list == null)
                {
                    ParameterObjectCollection pObjColl = ParameterObjectCollection.FromP3DFile(name);
                    if (pObjColl != null)
                    {
                        list = new List<ParamModVis3D>();
                        foreach (ParameterObjectData pOD in pObjColl.Children) list.Add(new ParamModVis3D(pOD.GetParamData()));
                    }
                }
                if (list != null) foreach (ParamModVis3D p in list) AddObject(p);
                else MessageBox.Show("Processing of P3D File was unsuccessful.");
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Open P3D File Exception: " + ex.ToString());
            }
        }

        private void splitObjectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Selection == null) return;
                List<ParamModVis3D> list = ParamModVis3D.Split(Selection);
                if (list == null || list.Count == 0) return;
                modVis3D.Children.Remove(Selection);
                foreach (ParamModVis3D p in list) AddObject(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Split Object Exception: " + ex.ToString());
            }
        }

        private void resetObjectTransformMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            Selection.TransformMatrix = Matrix3D.Identity;
        }

        private void resetGroupTransformMenuItem_Click(object sender, RoutedEventArgs e)
        {
            modVis3D.Transform = Transform3D.Identity;
        }

        private void displayTransformMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            Matrix3D m = Selection.TransformMatrix;
            MessageBox.Show("Current Object Transform Matrix:"
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.M11, m.M12, m.M13)
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.M21, m.M22, m.M23)
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.M31, m.M32, m.M33)
                + "\n" + string.Format("{0,10:F4}   {1,10:F4}   {2,10:F4}", m.OffsetX, m.OffsetY, m.OffsetZ));
        }



        private void changeColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            Selection.ColorString = (string)comboboxColor.SelectedItem;
        }

        private void changeBackColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            Selection.BackColorString = (string)comboboxColor.SelectedItem;
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
                MessageBox.Show("Save Image exception: " + except.Message);
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
            Instructions instructions = new Instructions();
            instructions.ShowDialog();
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
            if (result != true) return;

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

            try
            {
                OpenTemplateDialog openTemplateDlg = new OpenTemplateDialog(pObjTemplate);
                bool? result = openTemplateDlg.ShowDialog();
                if (result != true)
                {
                    MessageBox.Show("No ParameterModel3D was created.");
                    return;
                }
                ParamModVis3D pmv3D = new ParamModVis3D((ParamData)openTemplateDlg.Tag);
                AddObject(pmv3D);
                return;
            }
            catch(Exception ex)
            {
                MessageBox.Show("Template processing failed." + ex.ToString());
                return;
            }

        }

        public void templateMenuItems_Click(object sender, RoutedEventArgs e)
        {
                string templateFileName = (string)((MenuItem)sender).Tag;
                OpenTemplate(templateFileName);
        }

        public void shapeMenuItems_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string shapeFileName = (string)((MenuItem)sender).Tag;
                string colstr = (string)comboboxColor.SelectedItem;
                List<ParamModVis3D> list = ParamModVis3D.FromP3DFile(shapeFileName);
                if (list == null)
                {
                    ParameterObjectCollection pObjColl = ParameterObjectCollection.FromP3DFile(shapeFileName);
                    if (pObjColl != null && pObjColl.Children != null && pObjColl.Children.Count > 0)
                    {
                        list = new List<ParamModVis3D>();
                        foreach (ParameterObjectData pOD in pObjColl.Children) list.Add(new ParamModVis3D(pOD.GetParamData()));
                    }
                }
                if (list != null && list.Count == 1)
                {
                    list[0].ColorString = colstr;
                    AddObject(list[0]);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Shape Menu Exception: " + ex.ToString());
            }
        }

        private void combineObjectsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (selectionList.Count <= 1) return;
            ParamModVis3D pmv3D = ParamModVis3D.Combine(selectionList);
            foreach (ParamModVis3D p in selectionList) modVis3D.Children.Remove(p);
            AddObject(pmv3D);
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
            try
            {
                Button b = (Button)sender;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string filename = baseDir + "\\StockPSDFiles\\" + (string)b.Tag;
                string colStr = (string)comboboxColor.SelectedItem;
                List<ParamModVis3D> list = ParamModVis3D.FromP3DFile(filename);
                if (list == null)
                {
                    ParameterObjectCollection pObjColl = ParameterObjectCollection.FromP3DFile(filename);
                    if (pObjColl.Children != null && pObjColl.Children.Count > 0)
                    {
                        list = new List<ParamModVis3D>();
                        foreach (ParameterObjectData pOD in pObjColl.Children) list.Add(new ParamModVis3D(pOD.GetParamData()));
                    }
                }
                if (list != null && list.Count == 1)
                {
                    list[0].ColorString = colStr;
                    AddObject(list[0]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Shape Toolbar Exception: " + ex.ToString());
            }
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
            if (Selection == null || Selection.ParamDataContent.Children.Count>0) return;
            AddImageDialog dlg = new AddImageDialog(Selection);
            bool? result = dlg.ShowDialog();
        }

        private void removeImageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Selection.ImageSource = null;
        }

        private void transformImageEventHandler(object sender, RoutedEventArgs e)
        {
            Matrix m;
            if (Selection == null || Selection.ImageSource == null) return;
            if (sender == flipSMenuItem) m = new Matrix(-1, 0, 0, 1, 0, 0);
            else if (sender == flipTMenuItem) m = new Matrix(1, 0, 0, -1, 0, 0);
            else if (sender == rotateImageMenuItem) m = (new RotateTransform(90)).Value;
            else return;
            Selection.ImageTransformMatrix = Selection.ImageTransformMatrix *  (new Matrix(1,0,0,1,-0.5,-0.5)) 
                * m * (new Matrix(1,0,0,1,0.5,0.5));
        }

        private void toggleTilingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null || Selection.ImageSource == null) return;
            Selection.ImageTiled = !Selection.ImageTiled;
        }

        private void moveToBackgroundMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            modVis3D.Children.Remove(Selection);
            Selection.Highlighted = false;
            Selection.TransformMatrix = Selection.TransformMatrix * modVis3D.Transform.Value;
            backgroundModVis3D.Children.Add(Selection);
            Selection = modVis3D.Children.Count > 0 ? (ParamModVis3D)modVis3D.Children[modVis3D.Children.Count - 1] : null;
        }

        private void retrieveAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (backgroundModVis3D.Children.Count == 0) return;
            Visual3D[] v3DArray = backgroundModVis3D.Children.ToArray();
            backgroundModVis3D.Children.Clear();
            Matrix3D invertMatrix = modVis3D.Transform.Value;
            invertMatrix.Invert();
            foreach (Visual3D v3D in v3DArray)
            {
                ((ParamModVis3D)v3D).TransformMatrix = ((ParamModVis3D)v3D).TransformMatrix * invertMatrix;
                AddObject((ParamModVis3D)v3D);
            }
        }

        private void toggleTranslucencyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            Selection.Opaque = !Selection.Opaque;
            modVis3D.Children.Remove(Selection);
            if (Selection.Opaque) modVis3D.Children.Insert(0, Selection);
            else modVis3D.Children.Add(Selection);
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

        private void cloneObjectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            try
            {
                ParamModVis3D pmv3D = new ParamModVis3D(Selection);
                AddObject(pmv3D);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Clone Object Exception: " + ex.ToString());
            }
        }

        private void tbxName_KeyDown(object sender, KeyEventArgs e)
        {
            if (Selection != null && e.Key == Key.Return) Selection.Name = tbxName.Text;
        }

        private void HighlightEventHandler(object sender, RoutedEventArgs e)
        {
            UpdateHighlights();
        }

        private void UpdateHighlights()
        {
            if (modVis3D.Children.Count == 0) return;
            foreach (Visual3D v3D in modVis3D.Children)
                ((ParamModVis3D)v3D).Highlighted = rbnNoHighlight.IsChecked == false && selectionList.Contains((ParamModVis3D)v3D) &&
                    (Selection == (ParamModVis3D)v3D || rbnListHighlight.IsChecked == true);
        }

        private void TextBox_DoubleClick(object sender, MouseEventArgs e)
        {
            if (sender is TextBox) ((TextBox)sender).Clear();
        }

        private void customColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Selection == null) return;
            Colordialog dlg = new Colordialog(Selection);
            dlg.ShowDialog();
        }

    }
}
