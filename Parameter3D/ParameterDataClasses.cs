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
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace Parameter3D
{
    [Serializable]
    public class ParameterObjectData
    {
        public string xFunction;
        public string yFunction;
        public string zFunction;
        public double Smin;
        public double Smax;
        public double Tmin;
        public double Tmax;
        public int NumFacetS;
        public int NumFacetT;
        public bool WrapS;
        public bool WrapT;
        public string colorString;
        public Matrix3D transformMatrix;
        public List<ParameterObjectData> Children;

        //Version 2 Fields
        [OptionalField(VersionAdded = 2)] Byte[] imageArray = null;
        [OptionalField(VersionAdded = 2)] [NonSerialized] public BitmapSource imageSource = null;
        [OptionalField(VersionAdded = 2)] public int[] imageIndices = new int[]{0,0,0,0};
        [OptionalField(VersionAdded = 2)] public string backColorString = "DarkGray";
        [OptionalField(VersionAdded = 2)] public bool translucent = false;
        [OptionalField(VersionAdded = 2)] public double xCenter = 0;
        [OptionalField(VersionAdded = 2)] public double yCenter = 0;
        [OptionalField(VersionAdded = 2)]  public double zCenter = 0;
        [OptionalField(VersionAdded = 2)] public Matrix imageTransformMatrix = Matrix.Identity;
        [OptionalField(VersionAdded = 2)] public bool imageTiled = false;

        public ParameterObjectData(string xf, string yf, string zf, double smin, double smax, double tmin, double tmax, 
            int nFacS, int nFacT, bool ws, bool wt, string cStr)
        {
            xFunction = xf;
            yFunction = yf;
            zFunction = zf;
            Smin = smin;
            Smax = smax;
            Tmin = tmin;
            Tmax = tmax;
            NumFacetS = nFacS;
            NumFacetT = nFacT;
            WrapS = ws;
            WrapT = wt;
            colorString = cStr;
            transformMatrix = Matrix3D.Identity;
            Children = new List<ParameterObjectData>();
        }


        public ParameterObjectData()
        {
            transformMatrix = Matrix3D.Identity;
            Children = new List<ParameterObjectData>();
        }


        public ModelVisual3D GetModelVisual3D()
        {
            Model3D mod3D = this.GetModel3D();
            if (mod3D == null) return null;
            ModelVisual3D mVis3D = new ModelVisual3D();
            mVis3D.Transform = mod3D.Transform;  
            mod3D.Transform = Transform3D.Identity;  
            mVis3D.Content = mod3D;
            return mVis3D;
        }

        private Model3D GetModel3D()
        {
            try
            {
                if (this.Children.Count == 0)
                {
                    MeshGeometry3D meshGeom = this.GetMeshGeometry3D();
                    MaterialGroup materialGroup = new MaterialGroup();
                    PropertyInfo prop = typeof(Colors).GetProperty(this.colorString);
                    Color color = (Color)prop.GetValue(null, null);
                    prop = typeof(Colors).GetProperty(this.backColorString);
                    Color backColor = (Color)prop.GetValue(null, null);
                    DiffuseMaterial material = new DiffuseMaterial(new SolidColorBrush(color)) ;
                    DiffuseMaterial backMaterial = new DiffuseMaterial(new SolidColorBrush(backColor));
                    if (translucent)
                    {
                        ((SolidColorBrush)material.Brush).Opacity = 0.3;
                        ((SolidColorBrush)backMaterial.Brush).Opacity = 0.3;
                    }

                    materialGroup.Children.Add(material);
                    materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush()));
                    materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 30));
                    materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush()));

                    GeometryModel3D geomMod3D = new GeometryModel3D(meshGeom, materialGroup);

                    if (imageSource != null) AddImageToGeometryModel3D(geomMod3D);

                    MaterialGroup backMaterialGroup = new MaterialGroup();
                    backMaterialGroup.Children.Add(backMaterial);
                    backMaterialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 30));
                    geomMod3D.BackMaterial = backMaterialGroup;

                    geomMod3D.Transform = new MatrixTransform3D(this.transformMatrix); //new code
                    return geomMod3D;
                }
                else
                {
                    Model3DGroup mod3DGroup = new Model3DGroup();
                    for (int i = 0; i < this.Children.Count; i++)
                    {
                        Model3D mod3D = this.Children[i].GetModel3D();
                        if (mod3D == null) return null;
                        mod3DGroup.Children.Add(mod3D);
                    }
                    mod3DGroup.Transform = new MatrixTransform3D(this.transformMatrix);
                    return mod3DGroup;
                }
            }
            catch (Exception except)
            {
                MessageBox.Show("Error: " + except.Message);
                return null;
            }
        }

    

        public virtual MeshGeometry3D GetMeshGeometry3D()
        {
            int Ns = this.NumFacetS + 1;    
            int Nt = this.NumFacetT + 1;    

            ExpressionParser xExpr = new ExpressionParser(this.xFunction, "s", "t");
            ExpressionParser yExpr = new ExpressionParser(this.yFunction, "s", "t");
            ExpressionParser zExpr = new ExpressionParser(this.zFunction, "s", "t");

            Func<double, double, double> xFunc = (double s, double t) => xExpr.runnable(new double[] { s, t });
            Func<double, double, double> yFunc = (double s, double t) => yExpr.runnable(new double[] { s, t });
            Func<double, double, double> zFunc = (double s, double t) => zExpr.runnable(new double[] { s, t });

            MeshGeometry3D meshGeom = new MeshGeometry3D();
            for (int i = 0; i < Ns; i++)
                for (int j = 0; j < Nt; j++)
                {
                    double s = this.Smin + (double)i * (this.Smax - this.Smin) / (double)this.NumFacetS;
                    double t = this.Tmin + (double)j * (this.Tmax - this.Tmin) / (double)this.NumFacetT;
                    if ((i < (Ns - 1) || !WrapS) && (j < (Nt - 1) || !WrapT)) meshGeom.Positions.Add(new Point3D(xFunc(s, t), yFunc(s, t), zFunc(s, t)));
                    else if (i < (Ns - 1) || !WrapS) meshGeom.Positions.Add(meshGeom.Positions[GetPositionIndex(i, 0, Ns, Nt)]);
                    else if (j < (Nt - 1) || !WrapT) meshGeom.Positions.Add(meshGeom.Positions[GetPositionIndex(0, j, Ns, Nt)]);
                    else meshGeom.Positions.Add(meshGeom.Positions[GetPositionIndex(0, 0, Ns, Nt)]);
                }

            meshGeom.TriangleIndices.Clear();
            for (int col = 0; col < this.NumFacetS; col++)
                for (int row = 0; row < this.NumFacetT; row++)
                {
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col, row, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col + 1, row, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col + 1, row + 1, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col, row, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col + 1, row + 1, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col, row + 1, Ns, Nt));
                }

            AddNormalsToMeshGeometry3D(meshGeom, Ns, Nt, WrapS, WrapT);

            return meshGeom;

        }


        public static int GetPositionIndex(int i, int j, int Ns, int Nt)
        {
            return i * Nt + j;
        }

        public static void AddNormalsToMeshGeometry3D(MeshGeometry3D meshGeom, int Ns, int Nt, bool WrapS, bool WrapT)
        {
            meshGeom.Normals.Clear();
            for (int index = 0; index < meshGeom.Positions.Count; index++)
            {
                int i = index / Nt;
                int j = index % Nt;

                int i1, i2, i3, i4, i5, i6, j1, j2, j3, j4, j5, j6;
                if (i == 0) { i1 = i2 = 1; i3 = 0; i4 = i5 = Ns - 2; i6 = 0; }
                else if (i == (Ns - 1)) { i1 = i2 = 1; i3 = Ns - 1; i4 = i5 = Ns - 2; i6 = Ns - 1; }
                else { i1 = i2 = i + 1; i3 = i; i4 = i5 = i - 1; i6 = i; }

                if (j == 0) { j1 = 0; j2 = j3 = 1; j4 = 0; j5 = j6 = Nt - 2; }
                else if (j == (Nt - 1)) { j1 = Nt - 1; j2 = j3 = 1; j4 = Nt - 1; j5 = j6 = Nt - 2; }
                else { j1 = j; j2 = j3 = j + 1; j4 = j; j5 = j6 = j - 1; }

                int n0, n1, n2, n3, n4, n5, n6;
                n0 = GetPositionIndex(i, j, Ns, Nt);
                n1 = GetPositionIndex(i1, j1, Ns, Nt);
                n2 = GetPositionIndex(i2, j2, Ns, Nt);
                n3 = GetPositionIndex(i3, j3, Ns, Nt);
                n4 = GetPositionIndex(i4, j4, Ns, Nt);
                n5 = GetPositionIndex(i5, j5, Ns, Nt);
                n6 = GetPositionIndex(i6, j6, Ns, Nt);


                Vector3D[] v = new Vector3D[6];
                v[0] = WrapS || i < (Ns - 1) ? meshGeom.Positions[n1] - meshGeom.Positions[n0] : new Vector3D(0, 0, 0);
                v[1] = (WrapS || i < (Ns - 1)) && (WrapT || j < (Nt - 1)) ? meshGeom.Positions[n2] - meshGeom.Positions[n0] : new Vector3D(0, 0, 0);
                v[2] = WrapT || j < (Nt - 1) ? meshGeom.Positions[n3] - meshGeom.Positions[n0] : new Vector3D(0, 0, 0);
                v[3] = WrapS || i > 0 ? meshGeom.Positions[n4] - meshGeom.Positions[n0] : new Vector3D(0, 0, 0);
                v[4] = (WrapS || i > 0) && (WrapT || j > 0) ? meshGeom.Positions[n5] - meshGeom.Positions[n0] : new Vector3D(0, 0, 0);
                v[5] = WrapT || j > 0 ? meshGeom.Positions[n6] - meshGeom.Positions[n0] : new Vector3D(0, 0, 0);

                Vector3D[] norms = new Vector3D[6];
                Vector3D nextNormal = new Vector3D(0, 0, 0);
                for (int k = 0; k < 6; k++)
                {
                    norms[k] = k < 5 ? Vector3D.CrossProduct(v[k], v[k + 1]) : Vector3D.CrossProduct(v[5], v[0]);
                    if (norms[k].Length > 0.001) norms[k].Normalize();
                    nextNormal = nextNormal + norms[k];
                }
                nextNormal.Normalize();
                meshGeom.Normals.Add(nextNormal);
            }
        }

        public void AddImageToGeometryModel3D(GeometryModel3D geomMod3D)
        {
            MeshGeometry3D meshGeom = (MeshGeometry3D)geomMod3D.Geometry;
            MaterialGroup materialGroup = (MaterialGroup)geomMod3D.Material;
            int iBegin = imageIndices[0];
            int jBegin = imageIndices[1];
            int iEnd = imageIndices[2];
            int jEnd = imageIndices[3];
            int Ns = NumFacetS + 1;
            int Nt = NumFacetT + 1;
            if (iBegin >= iEnd || jBegin >= jEnd) throw (new Exception("Invalid Index Range"));
            meshGeom.TextureCoordinates.Clear();
            for (int n = 0; n < meshGeom.Positions.Count; n++) meshGeom.TextureCoordinates.Add(new Point(-1, -1));
            ImageBrush imgBrush = new ImageBrush(imageSource);
            imgBrush.ViewportUnits = BrushMappingMode.Absolute;
            imgBrush.Transform = new MatrixTransform(imageTransformMatrix);
            if (imageTiled) imgBrush.TileMode = TileMode.Tile; else imgBrush.TileMode = TileMode.None;
            for (int i = 0; i < Ns; i++)
                for (int j = 0; j < Nt; j++)
                {
                    meshGeom.TextureCoordinates[ParameterObjectData.GetPositionIndex(i, j, Ns, Nt)] =
                        new Point((double)(i - iBegin) / (double)(iEnd - iBegin), (double)(j - jBegin) / (double)(jEnd - jBegin));
                }
            materialGroup.Children[1] = new DiffuseMaterial(imgBrush);
        }

        public void AddImage(BitmapSource imgSource, int iBegin, int jBegin, int iEnd, int jEnd)
        {
            imageSource = imgSource;
            imageIndices[0] = iBegin;
            imageIndices[1] = jBegin;
            imageIndices[2] = iEnd;
            imageIndices[3] = jEnd;
        }

        public void RemoveImage()
        {
            imageSource = null;
            imageTransformMatrix = Matrix.Identity;
            imageTiled = false;
        }


        public void TransformImage(GeometryModel3D geomMod3D, Matrix m)
        {
            try
            {
                if (imageSource == null) return;
                Material material = geomMod3D.Material;
                imageTransformMatrix = imageTransformMatrix * (new Matrix(1, 0, 0, 1, -.5, -.5)) * m * (new Matrix(1, 0, 0, 1, .5, .5));
                ImageBrush imgBrush = (ImageBrush)( (DiffuseMaterial)((MaterialGroup)material).Children[1] ).Brush;
                imgBrush.Transform = new MatrixTransform(imageTransformMatrix);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Image Transform was unsuccessful" + ex.ToString());
                return;
            }
            
        }

        public void TileImage(GeometryModel3D geomMod3D)
        {
            if (imageSource == null) return;
            if (imageTiled) return;
            Material material = geomMod3D.Material;
            ImageBrush imgBrush = (ImageBrush)((DiffuseMaterial)((MaterialGroup)material).Children[1]).Brush;
            imgBrush.TileMode = TileMode.Tile;
            imageTiled = true;
        }

        public void UnTileImage(GeometryModel3D geomMod3D)
        {
            if (imageSource == null) return;
            if (!imageTiled) return;
            Material material = geomMod3D.Material;
            ImageBrush imgBrush = (ImageBrush)((DiffuseMaterial)((MaterialGroup)material).Children[1]).Brush;
            imgBrush.TileMode = TileMode.None;
            imageTiled = false;
        }

        public void ChangeColor(String colStr)
        {
            if (String.IsNullOrEmpty(colStr)) return;
            if (this.Children == null || this.Children.Count == 0) this.colorString = colStr;
            else foreach (ParameterObjectData pOD in this.Children) pOD.ChangeColor(colStr);
        }

        public void ChangeBackColor(String backColStr)
        {
            if (String.IsNullOrEmpty(backColStr)) return;
            if (this.Children == null || this.Children.Count == 0) this.backColorString = backColStr;
            else foreach (ParameterObjectData pOD in this.Children) pOD.ChangeBackColor(backColStr);
        }

        public bool Translucent
        {
            get { return translucent; }
            set
            {
                translucent = value;
                if (Children != null && Children.Count > 0) foreach (ParameterObjectData pOd in Children) pOd.Translucent = value;
            }
        }

        public virtual ParamData GetParamData()
        {
            try
            {
                ParamData pd = new ParamData(xFunction, yFunction, zFunction, Smin.ToString(), Smax.ToString(),
                    Tmin.ToString(), Tmax.ToString(), NumFacetS, NumFacetT, colorString, null);
                pd.BackColorString = backColorString;
                pd.Opaque = !translucent;
                pd.ImageSource = imageSource;
                pd.ImageTiled = imageTiled;
                pd.ImageTransformMatrix = imageTransformMatrix;
                pd.TransformMatrix = transformMatrix;
                if (Children != null && Children.Count > 0) foreach (ParameterObjectData p in Children) pd.Children.Add(p.GetParamData());
                return pd;
            }
            catch { return null; }
        }
 


        [OnSerializing]
        void OnSerializing(StreamingContext sc)
        {
            if (imageSource != null)
            {
                MemoryStream stream = new MemoryStream();
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(imageSource));
                encoder.Save(stream);
                stream.Flush();
                imageArray = stream.ToArray();
            }
        }

        [OnSerialized]
        void OnSerialized(StreamingContext sc)
        {
            imageArray = null;
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext sc)
        {
            if (imageArray != null)
            {
                MemoryStream stream = new MemoryStream(imageArray);
                BmpBitmapDecoder decoder = new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                imageSource = decoder.Frames[0];
                imageArray = null;
            }
            if (imageIndices == null) imageIndices = new int[] { 0, 0, 0, 0 };
            if (imageSource == null) imageTransformMatrix = Matrix.Identity;
            if (imageSource == null) imageTiled = false;
            if (String.IsNullOrEmpty(backColorString)) backColorString = "DarkGray";
        }
    }

    [Serializable]
    public class ParameterExtrusionObjectData : ParameterObjectData
    {
        public string xPrimeFunction;
        public string yPrimeFunction;
        
        public ParameterExtrusionObjectData(string xf, string yf, string zf, string xpf, string ypf, 
            double smin, double smax, double tmin, double tmax, int nFacS, int nFacT, bool ws, bool wt, string cStr)
            : base(xf, yf, zf, smin, smax, tmin, tmax, nFacS, nFacT, ws, wt, cStr)
        {
            xPrimeFunction = xpf;
            yPrimeFunction = ypf;
        }


        public override MeshGeometry3D GetMeshGeometry3D()
        {
            int Ns = this.NumFacetS+1;
            int Nt = this.NumFacetT+1;

            ExpressionParser xExpr = new ExpressionParser(this.xFunction, "s");
            ExpressionParser yExpr = new ExpressionParser(this.yFunction, "s");
            ExpressionParser zExpr = new ExpressionParser(this.zFunction, "s");
            ExpressionParser xPrExpr = new ExpressionParser(this.xPrimeFunction, "s", "t");
            ExpressionParser yPrExpr = new ExpressionParser(this.yPrimeFunction, "s", "t");

            Func<double, double> xFunc = (double s) => xExpr.runnable(new double[] { s });
            Func<double, double> yFunc = (double s) => yExpr.runnable(new double[] { s });
            Func<double, double> zFunc = (double s) => zExpr.runnable(new double[] { s });
            Func<double, double, double> xPrFunc = (double s, double t) => xPrExpr.runnable(new double[] { s, t });
            Func<double, double, double> yPrFunc = (double s, double t) => yPrExpr.runnable(new double[] { s, t });

            MeshGeometry3D meshGeom = new MeshGeometry3D();

            int i, j;
            double ds = (this.Smax - this.Smin) / (double)this.NumFacetS;
            double dt = (this.Tmax - this.Tmin) / (double)this.NumFacetT;

            Point3D[] center = new Point3D[Ns];
            for (i = 0; i < Ns; i++)
            {
                double s = this.Smin + (double)i * ds;
                center[i] = new Point3D(xFunc(s), yFunc(s), zFunc(s));
            }

            Vector3D[] dirVec = new Vector3D[Ns + 1];
            dirVec[0] = WrapS? Point3D.Subtract(center[Ns-1],center[Ns-2]) :
                Point3D.Subtract(center[0], new Point3D(xFunc(this.Smin - ds), yFunc(this.Smin - ds), zFunc(this.Smin - ds)));
            if (dirVec[0].Length < 0.0000001) throw new Exception("Error: Gradient of extrusion curve is zero");
            dirVec[0].Normalize();
            for (i = 1; i < Ns; i++)
            {
                dirVec[i] = Point3D.Subtract(center[i], center[i - 1]);
                if (dirVec[i].Length < 0.0000001) throw new Exception("Error: Gradient of extrusion curve is zero");
                dirVec[i].Normalize();
            }
            
            dirVec[Ns] = WrapS? Point3D.Subtract( center[1], center[0] ) :
                Point3D.Subtract(new Point3D(xFunc(this.Smax + ds), yFunc(this.Smax + ds), zFunc(this.Smax + ds)), center[Ns - 1]);
            if (dirVec[Ns].Length < 0.0000001) throw new Exception("Error: Gradient of extrusion curve is zero");
            dirVec[Ns].Normalize();

            Vector3D[] zPrimeAxis = new Vector3D[Ns];
            for (i = 0; i < Ns; i++)
            {
                zPrimeAxis[i] = Vector3D.Add(dirVec[i], dirVec[i + 1]) / 2;
                zPrimeAxis[i].Normalize();
                zPrimeAxis[i].Negate();
            }

            Vector3D[] xPrimeAxis = new Vector3D[Ns];
            Vector3D[] yPrimeAxis = new Vector3D[Ns];
            bool axisFound = false;
            int axisFoundIndex = 0;
            
            for (i = 0; i < Ns; i++)
            {
                xPrimeAxis[i] = Vector3D.CrossProduct(dirVec[i], dirVec[i + 1]);
                if (xPrimeAxis[i].Length < 0.0001)
                {
                    if (axisFound) xPrimeAxis[i] = xPrimeAxis[i - 1];
                    else continue;
                }
                else
                {
                    if (!axisFound)
                    {
                        axisFound = true;
                        axisFoundIndex = i;
                    }
                    xPrimeAxis[i].Normalize();
                    if (i > axisFoundIndex && Vector3D.DotProduct(xPrimeAxis[i], xPrimeAxis[i - 1]) < 0)
                    {
                        xPrimeAxis[i].Negate();
                    }
                }
            }

            if (axisFound)
            {
                if (axisFoundIndex > 0) for (i = 0; i < axisFoundIndex; i++)  xPrimeAxis[i] = xPrimeAxis[axisFoundIndex];
                int Navg = Ns/10;
                Vector3D[] xPrimeAxisAvg = new Vector3D[Ns];
                Vector3D[] yPrimeAxisAvg = new Vector3D[Ns];
                for (i = 0; i < Ns; i++)
                {
                    int jMin = i - Navg;
                    int jMax = i + Navg;
                    if (!WrapS)
                    {
                        jMin = jMin < 0 ? 0 : jMin;
                        jMax = jMax >= Ns ? Ns - 1 : jMax;
                    }
                    xPrimeAxisAvg[i] = new Vector3D(0, 0, 0);
                    for (j = jMin; j <= jMax; j++)
                    {
                        int jCorr = j % Ns;
                        if (jCorr < 0) jCorr += Ns;
                        xPrimeAxisAvg[i] = xPrimeAxisAvg[i] + xPrimeAxis[jCorr] - zPrimeAxis[i] * Vector3D.DotProduct(zPrimeAxis[i], xPrimeAxis[jCorr]);
                    }
                    xPrimeAxisAvg[i].Normalize();
                    yPrimeAxisAvg[i] = Vector3D.CrossProduct(zPrimeAxis[i], xPrimeAxisAvg[i]);
                    yPrimeAxisAvg[i].Normalize();
                }
                xPrimeAxis = xPrimeAxisAvg;
                yPrimeAxis = yPrimeAxisAvg;
            }
            else
            {
                xPrimeAxis[0] = Vector3D.CrossProduct(zPrimeAxis[0], new Vector3D(0, 0, 1));
                if (xPrimeAxis[0].Length < 0.1) xPrimeAxis[0] = Vector3D.CrossProduct(zPrimeAxis[0], new Vector3D(0, 1, 0));
                xPrimeAxis[0].Normalize();
                yPrimeAxis[0] = Vector3D.CrossProduct(zPrimeAxis[0], xPrimeAxis[0]);
                yPrimeAxis[0].Normalize();
                for (i = 1; i < Ns; i++)
                {
                    xPrimeAxis[i] = xPrimeAxis[0];
                    yPrimeAxis[i] = yPrimeAxis[0];
                }
            }
            

            for (i = 0; i < Ns; i++)
                for (j = 0; j < Nt; j++)
                {
                    double s = this.Smin + (double)i * ds;
                    double t = this.Tmin + (double)j * dt;
                    Vector3D xPrimeVector = Vector3D.Multiply(xPrFunc(s, t), xPrimeAxis[i]);
                    Vector3D yPrimeVector = Vector3D.Multiply(yPrFunc(s, t), yPrimeAxis[i]);
                    if ((i < (Ns - 1) || !WrapS) && (j < (Nt - 1) || !WrapT))
                    {
                        Point3D pos = Point3D.Add(center[i], Vector3D.Add(xPrimeVector, yPrimeVector));
                        meshGeom.Positions.Add(pos);
                    }
                    else if (i < (Ns - 1) || !WrapS) meshGeom.Positions.Add(meshGeom.Positions[GetPositionIndex(i, 0, Ns, Nt)]);
                    else if (j < (Nt - 1) || !WrapT) meshGeom.Positions.Add(meshGeom.Positions[GetPositionIndex(0, j, Ns, Nt)]);
                    else meshGeom.Positions.Add(meshGeom.Positions[GetPositionIndex(0, 0, Ns, Nt)]);
                }

            meshGeom.TriangleIndices.Clear();
            for (int col = 0; col < this.NumFacetS; col++)
                for (int row = 0; row < this.NumFacetT; row++)
                {
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col, row, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col + 1, row, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col + 1, row + 1, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col, row, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col + 1, row + 1, Ns, Nt));
                    meshGeom.TriangleIndices.Add(GetPositionIndex(col, row + 1, Ns, Nt));
                }

            AddNormalsToMeshGeometry3D(meshGeom, Ns, Nt, WrapS, WrapT);

            return meshGeom;
        }

        public override ParamData GetParamData()
        {
            try
            {
                ParamData pd = new ParamExtrusionData(xFunction, yFunction, zFunction, xPrimeFunction, yPrimeFunction, Smin.ToString(), Smax.ToString(),
                    Tmin.ToString(), Tmax.ToString(), NumFacetS, NumFacetT, colorString, null);
                pd.BackColorString = backColorString;
                pd.Opaque = !translucent;
                pd.ImageSource = imageSource;
                pd.ImageTiled = imageTiled;
                pd.ImageTransformMatrix = imageTransformMatrix;
                pd.TransformMatrix = transformMatrix;
                if (Children != null && Children.Count > 0) foreach (ParameterObjectData p in Children) pd.Children.Add(p.GetParamData());
                return pd;
            }
            catch { return null; }
        }
 


    }

    [Serializable]
    public class ParameterObjectCollection
    {
        List<ParameterObjectData> parameterObjectList;

        public ParameterObjectCollection()
        {
            parameterObjectList = new List<ParameterObjectData>();
        }

        public List<ParameterObjectData> Children
        {
            get
            {
                return parameterObjectList;
            }
        }

        public static ParameterObjectCollection FromP3DFile(string fname)
        {
            FileStream infile = null;
            BinaryFormatter bf;
            ParameterObjectCollection pobj;
            if (string.IsNullOrEmpty(fname)) return null;
            string ext = System.IO.Path.GetExtension(fname);
            if (string.IsNullOrEmpty(ext) || ext != ".p3d") return null;
            try
            {
                bf = new BinaryFormatter();
                infile = new FileStream(fname, FileMode.Open, FileAccess.Read);
                pobj = (ParameterObjectCollection)bf.Deserialize(infile);
            }
            catch(Exception e)
            {
                MessageBox.Show("Error opening file or deserializing.  No ParameterObjectCollection created.\n" + e.ToString());
                return null;
            }
            finally
            {
                if (infile != null) infile.Close();
            }
            return pobj;
        }
    }

    [Serializable]
    public class ParameterObjectTemplate
    {
        public string name, description, xExpr, yExpr, zExpr, SminExpr, SmaxExpr, TminExpr, TmaxExpr, nsExpr, ntExpr;
        public bool WrapS, WrapT;
        public List<ParameterObjectTemplate> Children;
        public string[] ParamNames;

        public ParameterObjectTemplate(string nm, string descr, string xf, string yf, string zf, string smin, string smax, string tmin, string tmax, 
            string nFacS, string nFacT, bool ws, bool wt, params string[] pnames)
        {
            name = nm;
            description = descr;
            xExpr = xf;
            yExpr = yf;
            zExpr = zf;
            SminExpr = smin;
            SmaxExpr = smax;
            TminExpr = tmin;
            TmaxExpr = tmax;
            nsExpr = nFacS;
            ntExpr = nFacT;
            WrapS = ws;
            WrapT = wt;
            Children = new List<ParameterObjectTemplate>();
            ParamNames = pnames;
        }


        public ParameterObjectTemplate(string nm, params string[] pnames)
        {
            name = nm;
            Children = new List<ParameterObjectTemplate>();
            ParamNames = pnames;
        }

        public virtual ParameterObjectData GetParameterObjectData( string colStr, params string[] paramValues )
        {

            if (Children == null || Children.Count == 0)
                return new ParameterObjectData(ReplaceNames(xExpr, ParamNames, paramValues),
                    ReplaceNames(yExpr, ParamNames, paramValues),
                    ReplaceNames(zExpr, ParamNames, paramValues),
                    (new ExpressionParser(ReplaceNames(SminExpr, ParamNames, paramValues), null)).runnable(null),
                    (new ExpressionParser(ReplaceNames(SmaxExpr, ParamNames, paramValues), null)).runnable(null),
                    (new ExpressionParser(ReplaceNames(TminExpr, ParamNames, paramValues), null)).runnable(null),
                    (new ExpressionParser(ReplaceNames(TmaxExpr, ParamNames, paramValues), null)).runnable(null),
                    (int)(new ExpressionParser(ReplaceNames(nsExpr, ParamNames, paramValues), null)).runnable(null),
                    (int)(new ExpressionParser(ReplaceNames(ntExpr, ParamNames, paramValues), null)).runnable(null),
                    WrapS, WrapT, colStr);
            else
            {
                ParameterObjectData pObjData = new ParameterObjectData();
                for (int i = 0; i < Children.Count; i++)
                    pObjData.Children.Add(Children[i].GetParameterObjectData(colStr, paramValues));
                return pObjData;
            }
        }

        public virtual ParamData GetParamData(string colStr, params string[] paramValues)
        {

            if (Children == null || Children.Count == 0)
                return new ParamData(ReplaceNames(xExpr, ParamNames, paramValues),
                    ReplaceNames(yExpr, ParamNames, paramValues),
                    ReplaceNames(zExpr, ParamNames, paramValues),
                    ReplaceNames(SminExpr, ParamNames, paramValues),
                    ReplaceNames(SmaxExpr, ParamNames, paramValues),
                    ReplaceNames(TminExpr, ParamNames, paramValues),
                    ReplaceNames(TmaxExpr, ParamNames, paramValues),
                    (int)(new ExpressionParser(ReplaceNames(nsExpr, ParamNames, paramValues), null)).runnable(null),
                    (int)(new ExpressionParser(ReplaceNames(ntExpr, ParamNames, paramValues), null)).runnable(null),
                    colStr, null);
            else
            {
                ParamData pd = new ParamData(null, colStr, ParamModVis3D.DefaultBackColorString, true, Matrix3D.Identity);
                for (int i = 0; i < Children.Count; i++)
                    pd.Children.Add(Children[i].GetParamData(colStr, paramValues));
                return pd;
            }
        }


        public static string ReplaceNames(string s, string[] names, string[] values)
        {
            int i;
            StringBuilder sb = new StringBuilder();
            i = 0;

            if (names == null || values == null || names.Length != values.Length) throw (new Exception("Parameter Name versus Value array mismatch"));

            while (i < s.Length)
            {
                if (!char.IsLetter(s[i]))
                {
                    sb.Append(s[i]);
                    i++;
                    continue;
                }

                string tempName = ExpressionParser.GetName(s, ref i, '\0');
                string tempValue = tempName;

                for (int index = 0; index < names.Length; index++)
                {
                    if (tempName == names[index])
                    {
                        tempValue = values[index];
                        break;
                    }
                }

                sb.Append(tempValue);
            }
            return sb.ToString();
        }

        public virtual bool IsValidTemplate()
        {
            if (ParamNames == null || ParamNames.Length == 0) return false;
            if (Children != null && Children.Count > 0)
            {
                for (int j = 0; j < Children.Count; j++)
                {
                    bool validChild = Children[j].IsValidTemplate();
                    if (!validChild) return false;
                }
                return true;
            }
            string[] extPNames2 = new string[ParamNames.Length + 2];
            for (int i=0; i<(ParamNames.Length+2); i++)
                if (i<ParamNames.Length) extPNames2[i] = ParamNames[i];
                else if (i==ParamNames.Length) extPNames2[i] = "s";
                else extPNames2[i] = "t";
            ExpressionParser dumExpr;
            try
            {
                dumExpr = new ExpressionParser(xExpr, extPNames2);
                dumExpr = new ExpressionParser(yExpr, extPNames2);
                dumExpr = new ExpressionParser(zExpr, extPNames2);
                dumExpr = new ExpressionParser(SminExpr, ParamNames);
                dumExpr = new ExpressionParser(SmaxExpr, ParamNames);
                dumExpr = new ExpressionParser(TminExpr, ParamNames);
                dumExpr = new ExpressionParser(TmaxExpr, ParamNames);
                dumExpr = new ExpressionParser(nsExpr, ParamNames);
                dumExpr = new ExpressionParser(ntExpr, ParamNames);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public void AddParamName( string newParamName )
        {
            string[] tempNames;
            if (ParamNames == null || ParamNames.Length == 0) 
            {
                tempNames = new string[1] {newParamName};
            }
            else
            {
                tempNames = new string[ParamNames.Length + 1];
                for (int i = 0; i < ParamNames.Length; i++) tempNames[i] = ParamNames[i];
                tempNames[ParamNames.Length] = newParamName;
            }
            ParamNames = tempNames;

            if (Children != null && Children.Count > 0)
                for (int j = 0; j < Children.Count; j++) Children[j].AddParamName(newParamName);
        }

        public void DeleteParamName(int index)
        {
            if (ParamNames == null || index>=ParamNames.Length || index<0) return;
            string[] tempNames = new string[ParamNames.Length - 1];
            if (tempNames.Length > 0)
            {
                for (int i = 0; i < tempNames.Length; i++)
                    if (i < index) tempNames[i] = ParamNames[i]; else tempNames[i] = ParamNames[i + 1];
            }
            ParamNames = tempNames;
            if (Children != null && Children.Count > 0)
                for (int j = 0; j < Children.Count; j++) Children[j].DeleteParamName(index);
        }

    }

    [Serializable]
    public class ParameterExtrusionObjectTemplate : ParameterObjectTemplate
    {
        public string xPrimeExpr, yPrimeExpr;

        public ParameterExtrusionObjectTemplate(string nm, string descr, string xf, string yf, string zf, string xpf, string ypf, string smin, string smax, string tmin, string tmax, 
            string nFacS, string nFacT, bool ws, bool wt, params string[] pnames) : base(nm, descr, xf, yf, zf, smin, smax, tmin, tmax, nFacS, nFacT, ws, wt, pnames)
        {
            xPrimeExpr = xpf;
            yPrimeExpr = ypf;
        }

        public override ParameterObjectData GetParameterObjectData(string colStr, params string[] paramValues)
        {
            if (Children != null && Children.Count != 0) return base.GetParameterObjectData(colStr, paramValues);
            else
                return new ParameterExtrusionObjectData(ReplaceNames(xExpr, ParamNames, paramValues),
                    ReplaceNames(yExpr, ParamNames, paramValues),
                    ReplaceNames(zExpr, ParamNames, paramValues),
                    ReplaceNames(xPrimeExpr, ParamNames, paramValues),
                    ReplaceNames(yPrimeExpr, ParamNames, paramValues),
                    (new ExpressionParser(ReplaceNames(SminExpr, ParamNames, paramValues), null)).runnable(null),
                    (new ExpressionParser(ReplaceNames(SmaxExpr, ParamNames, paramValues), null)).runnable(null),
                    (new ExpressionParser(ReplaceNames(TminExpr, ParamNames, paramValues), null)).runnable(null),
                    (new ExpressionParser(ReplaceNames(TmaxExpr, ParamNames, paramValues), null)).runnable(null),
                    (int)(new ExpressionParser(ReplaceNames(nsExpr, ParamNames, paramValues), null)).runnable(null),
                    (int)(new ExpressionParser(ReplaceNames(ntExpr, ParamNames, paramValues), null)).runnable(null),
                    WrapS, WrapT, colStr);
        }

        public override ParamData GetParamData(string colStr, params string[] paramValues)
        {

            if (Children == null || Children.Count == 0)
                return new ParamExtrusionData(ReplaceNames(xExpr, ParamNames, paramValues),
                    ReplaceNames(yExpr, ParamNames, paramValues),
                    ReplaceNames(zExpr, ParamNames, paramValues),
                    ReplaceNames(xPrimeExpr, ParamNames, paramValues),
                    ReplaceNames(yPrimeExpr, ParamNames, paramValues),
                    ReplaceNames(SminExpr, ParamNames, paramValues),
                    ReplaceNames(SmaxExpr, ParamNames, paramValues),
                    ReplaceNames(TminExpr, ParamNames, paramValues),
                    ReplaceNames(TmaxExpr, ParamNames, paramValues),
                    (int)(new ExpressionParser(ReplaceNames(nsExpr, ParamNames, paramValues), null)).runnable(null),
                    (int)(new ExpressionParser(ReplaceNames(ntExpr, ParamNames, paramValues), null)).runnable(null),
                    colStr, null);
            else
            {
                ParamData pd = new ParamData(null, colStr, ParamModVis3D.DefaultBackColorString, true, Matrix3D.Identity);
                for (int i = 0; i < Children.Count; i++)
                    pd.Children.Add(Children[i].GetParamData(colStr, paramValues));
                return pd;
            }

        }

        public override bool IsValidTemplate()
        {
            if (Children != null && Children.Count > 0) return base.IsValidTemplate();
            if (!base.IsValidTemplate()) return false;
            string[] extPNames1 = new string[ParamNames.Length + 1];
            string[] extPNames2 = new string[ParamNames.Length + 2];
            for (int i = 0; i < (ParamNames.Length); i++)
            {
                extPNames1[i] = ParamNames[i];
                extPNames2[i] = ParamNames[i];
            }
            extPNames1[ParamNames.Length] = "s";
            extPNames2[ParamNames.Length] = "s";
            extPNames2[ParamNames.Length + 1] = "t";
            ExpressionParser dumExpr;
            try
            {
                dumExpr = new ExpressionParser(xExpr, extPNames1);
                dumExpr = new ExpressionParser(yExpr, extPNames1);
                dumExpr = new ExpressionParser(zExpr, extPNames1);
                dumExpr = new ExpressionParser(xPrimeExpr, extPNames2);
                dumExpr = new ExpressionParser(yPrimeExpr, extPNames2);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }

}
