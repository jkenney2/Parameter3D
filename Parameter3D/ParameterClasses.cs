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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Parameter3D
{
    [Serializable]
    public class ParamModVis3D : ModelVisual3D, ISerializable
    {
        /*
         * ParamModVis3D is derived from ModelVisual3D and also encapsulates a ParamData object.
         * 
         * The ParamData object is accessed using ParamDataContent property (get only).
         * 
         * The Content property is a Model3D (either Model3DGroup or GeometryModel3D) object which has been generated using
         * the ParamData object.
         * 
         * The Children property of the ModelVisual3D base is hidden by a private (null) Children property, and is not used.
         * 
         * The TransformMatrix property mirrors the Transform property of the base object.  The TransformMatrix property of
         * ParamDataContent and the Transform property of Content must be Identity.  Any ParamModVis3D constructor and any method
         * returning a ParamModVis3D (or list or array of ParamModVis3D) must follow this convention.
         * 
         * The Name property simply mirrors the Name property of the ParamDataContent.
         * 
         * The 'get' accessors for the ColorString, BackColorString, Opaque, Highlighted, ImageSource, ImageIndices, ImageTiled,
         * and ImageTransformMatrix properties simply mirror the same-named properties of ParamDataContent.  The 'set' accessors
         * for these properties pass through to set the same-named properties of ParamDataContent, but they also make the
         * appropriate modifications to the Content property (specifically, to Content.Material).
         * 
         */
        new private Visual3DCollection Children { get { return null; } }  //Hides the Children collection of the ModelVisual3D base.
        
        public static readonly string DefaultBackColorString = "DarkGray";
        public static readonly double WrapThreshhold = 0.005;
        public static readonly double DefaultTranslucent = 0.3;
        public static readonly EmissiveMaterial HighlightMaterial = new EmissiveMaterial(new SolidColorBrush(Colors.White));
        public static readonly DiffuseMaterial BlankMaterial = new DiffuseMaterial(new SolidColorBrush());

        public string Name { get { return paramDataContent.Name; } set { paramDataContent.Name = value; } }

        public string ColorString 
        {
            get { return paramDataContent.ColorString; }
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                ParamDataContent.ColorString = value;
                SetModel3DColor(Content, value);
            }
        }

        public string BackColorString {
            get { return paramDataContent.BackColorString;}
            set 
            { 
                if (string.IsNullOrEmpty(value)) return;
                ParamDataContent.BackColorString = value;
                SetModel3DBackColor(Content, value);
            }
        }
        public bool Opaque
        { get { return paramDataContent.Opaque;}
            set 
            { 
                ParamDataContent.Opaque = value;
                SetModel3DOpacity(Content, value);
            }
        }
        public bool Highlighted
        {
            get { return paramDataContent.Highlighted; }
            set
            {
                paramDataContent.Highlighted = value;
                SetModel3DHighlight(Content, value);
            }
        }

        public Matrix3D TransformMatrix { get { return this.Transform.Value; } set { this.Transform = new MatrixTransform3D(value); } }

        public BitmapSource ImageSource
        {
            get { return ParamDataContent.ImageSource; }
            set
            {
                ParamDataContent.ImageSource = value;
                ParamDataContent.SetModel3DImage(Content);
            }
        }

        public int[] ImageIndices
        {
            get { return ParamDataContent.ImageIndices; }
            set
            {
                ParamDataContent.ImageIndices = value;
                ParamDataContent.SetModel3DImage(Content);
            }
        }

        public bool ImageTiled
        {
            get { return ParamDataContent.ImageTiled; }
            set
            {
                ParamDataContent.ImageTiled = value;
                ParamDataContent.SetModel3DImage(Content);
            }
        }

        public Matrix ImageTransformMatrix
        {
            get { return ParamDataContent.ImageTransformMatrix; }
            set
            {
                ParamDataContent.ImageTransformMatrix = value;
                ParamDataContent.SetModel3DImage(Content);
            }
        }

        protected ParamData paramDataContent;
        public ParamData ParamDataContent { get { return paramDataContent; } }


        public ParamModVis3D(ParamData pd)
        {
            paramDataContent = pd;
            Content = paramDataContent.GetModel3D();
            TransformMatrix = pd.TransformMatrix;
            paramDataContent.TransformMatrix = Matrix3D.Identity;
            Content.Transform = Transform3D.Identity;
        }

        public ParamModVis3D(ParamModVis3D pmv3D)
        {
            paramDataContent = pmv3D.paramDataContent.Clone();
            Content = paramDataContent.GetModel3D();
            TransformMatrix = pmv3D.TransformMatrix;
        }


        public static ParamModVis3D Combine(List<ParamModVis3D> list)
        {
            if (list == null) return null;
            if (list.Count == 0) return null;
            ParamData pd;
            if (list.Count == 1)
            {
                pd = list[0].ParamDataContent.Clone();
                pd.TransformMatrix = list[0].TransformMatrix;
            }
            else
            {
                pd = new ParamData(list[0].Name, list[0].ColorString, list[0].BackColorString, list[0].Opaque, list[0].TransformMatrix);
                Matrix3D invertMatrix = list[0].TransformMatrix;
                invertMatrix.Invert();
                foreach (ParamModVis3D pmv3D in list)
                {
                    ParamData p = pmv3D.ParamDataContent.Clone();
                    p.TransformMatrix = pmv3D.TransformMatrix * invertMatrix;
                    pd.Children.Add(p);
                }
            }
            return new ParamModVis3D(pd);
        }

        public static List<ParamModVis3D> Split(ParamModVis3D pmv3D)
        {
            if (pmv3D == null || pmv3D.paramDataContent == null) return null;
            if (pmv3D.paramDataContent.Children.Count == 0) return new List<ParamModVis3D>(){pmv3D};
            List<ParamModVis3D> list = new List<ParamModVis3D>();
            foreach (ParamData pd in pmv3D.paramDataContent.Children)
            {
                ParamData p = pd.Clone();
                p.TransformMatrix = p.TransformMatrix * pmv3D.TransformMatrix;
                list.Add(new ParamModVis3D(p));
            }
            return list;
        }

        protected void SetModel3DColor(Model3D m3D, string colStr)
        {
            if (m3D is Model3DGroup) foreach (Model3D m in ((Model3DGroup)m3D).Children) SetModel3DColor(m, colStr);
            else if (m3D is GeometryModel3D) ((SolidColorBrush)((DiffuseMaterial)((MaterialGroup)((GeometryModel3D)m3D).Material).Children[0]).Brush).Color = ColorFromString(colStr);
        }

        protected void SetModel3DBackColor(Model3D m3D, string backColStr)
        {
            if (m3D is Model3DGroup) foreach (Model3D m in ((Model3DGroup)m3D).Children) SetModel3DBackColor(m, backColStr);
            else if (m3D is GeometryModel3D) ((SolidColorBrush)((DiffuseMaterial)((MaterialGroup)((GeometryModel3D)m3D).BackMaterial).Children[0]).Brush).Color = ColorFromString(backColStr);
        }

        public static Color ColorFromString(string colstr)
        {
            if (colstr[0] == '#')
            {
                Byte R = (Byte)Convert.ToInt32(colstr.Substring(1, 2), 16);
                Byte G = (Byte)Convert.ToInt32(colstr.Substring(3, 2), 16);
                Byte B = (Byte)Convert.ToInt32(colstr.Substring(5, 2), 16);
                return Color.FromRgb(R, G, B);
            }
            PropertyInfo prop = typeof(Colors).GetProperty(colstr);
            return (Color)prop.GetValue(null, null);
        }

        protected void SetModel3DOpacity(Model3D m3D, bool opaque)
        {
            if (m3D is Model3DGroup) foreach (Model3D m in ((Model3DGroup)m3D).Children) SetModel3DOpacity(m, opaque);
            else if (m3D is GeometryModel3D)
            {
                ((SolidColorBrush)((DiffuseMaterial)((MaterialGroup)((GeometryModel3D)m3D).Material).Children[0]).Brush).Opacity
                = opaque ? 1.0 : 0.3;
                ((SolidColorBrush)((DiffuseMaterial)((MaterialGroup)((GeometryModel3D)m3D).BackMaterial).Children[0]).Brush).Opacity
                    = opaque ? 1.0 : 0.3;
            }
        }

        protected void SetModel3DHighlight(Model3D m3D, bool highlighted)
        {
            if (m3D is Model3DGroup) foreach (Model3D m in ((Model3DGroup)m3D).Children) SetModel3DHighlight(m, highlighted);
            else if (m3D is GeometryModel3D)
            {
                Material mat;
                if (highlighted) mat = HighlightMaterial; else mat = BlankMaterial;
                ((MaterialGroup)((GeometryModel3D)m3D).Material).Children[3] = mat;
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext sc)
        {
            info.AddValue("Version", 1);
            info.AddValue("paramDataContent", paramDataContent);
            info.AddValue("TransformMatrix", TransformMatrix);
        }

        protected ParamModVis3D(SerializationInfo si, StreamingContext sc)
        {
            paramDataContent = (ParamData)si.GetValue("paramDataContent", typeof (ParamData));
            Content = paramDataContent.GetModel3D();
            TransformMatrix = (Matrix3D)si.GetValue("TransformMatrix", typeof(Matrix3D));
        }

        public static List<ParamModVis3D> FromP3DFile(string fname)
        {
            FileStream infile = null;
            BinaryFormatter bf;
            List<ParamModVis3D> list;
            if (string.IsNullOrEmpty(fname)) return null;
            string ext = System.IO.Path.GetExtension(fname);
            if (string.IsNullOrEmpty(ext) || ext != ".p3d") return null;
            try
            {
                bf = new BinaryFormatter();
                infile = new FileStream(fname, FileMode.Open, FileAccess.Read);
                list = (List<ParamModVis3D>)bf.Deserialize(infile);
            }
            catch 
            {
                return null;
            }
            finally
            {
                if (infile != null) infile.Close();
            }
            return list;
        }

        public static bool Save(List<ParamModVis3D> list, string fname)
        {
            if (list == null || list.Count == 0) return false;
            BinaryFormatter bf = null;
            FileStream outfile = null;
            try
            {
                bf = new BinaryFormatter();
                outfile = new FileStream(fname, FileMode.Create, FileAccess.Write);
                bf.Serialize(outfile, list);
                outfile.Close();
                return true;
            }
            catch (Exception except)
            {
                MessageBox.Show("Save File exception: " + except.Message);
                if (outfile != null) outfile.Close();
                return false;
            }
        }
    }

    [Serializable]
    public class ParamData : ISerializable
    {
        protected string xExpr, yExpr, zExpr, sMin, sMax, tMin, tMax, colorString, name;
        protected int numFacetS, numFacetT;
        protected string backColorString = ParamModVis3D.DefaultBackColorString;
        protected bool opaque = true;
        protected bool highlighted = false;
        protected Matrix3D transformMatrix = Matrix3D.Identity;
        protected BitmapSource imageSource = null;
        protected int[] imageIndices = null;
        protected bool imageTiled = false;
        protected Matrix imageTransformMatrix = Matrix.Identity;
        protected double xCenter, yCenter, zCenter;

        protected List<ParamData> children;
        public List<ParamData> Children { get { return children; } }

        public string XExpr { get { return xExpr; } }
        public string YExpr { get { return yExpr; } }
        public string ZExpr { get { return zExpr; } }
        public string SMin { get { return sMin; } }
        public string SMax { get { return sMax; } }
        public string TMin { get { return tMin; } } 
        public string TMax { get { return tMax; } }
        public int NumFacetS { get { return numFacetS; } }
        public int NumFacetT { get { return numFacetT; } }
        public string Name { get { return name; } set { name = value; } }

        public Matrix3D TransformMatrix { get { return transformMatrix; } set { transformMatrix = value; } }

        public string ColorString
        {
            get { return colorString; }
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                colorString = value;
                if (children.Count > 0) foreach (ParamData p in children) p.ColorString = value;
            }
        }

        public string BackColorString
        {
            get { return backColorString; }
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                backColorString = value;
                if (children.Count > 0) foreach (ParamData p in children) p.BackColorString = value;
            }
        }

        public bool Opaque
        {
            get { return opaque; }
            set { opaque = value; if (children.Count > 0) foreach (ParamData p in children) p.Opaque = value; }
        }

        public bool Highlighted
        {
            get { return highlighted; }
            set { highlighted = value; if (children.Count > 0) foreach (ParamData p in children) p.Highlighted = value; }
        }

        public BitmapSource ImageSource { get { return imageSource; } set { if (children.Count > 0) imageSource = null; else imageSource = value;} }

        public int[] ImageIndices
        {
            get { return imageIndices; }
            set
            {
                if (children.Count > 0 || value == null || value.Length<4) imageIndices = null;
                else imageIndices = new int[4] { value[0], value[1], value[2], value[3] };
            }
        }

        public bool ImageTiled
        {
            get { return imageTiled; }
            set { if (children.Count > 0) imageTiled = false; else imageTiled = value; }
        }

        public Matrix ImageTransformMatrix
        {
            get { return imageTransformMatrix; }
            set { if (children.Count > 0) return; else imageTransformMatrix = value; }
        }

        public ParamData(string x, string y, string z, string s0, string s1, string t0, string t1, int nfs, int nft, string colstr, string nm)
        {
            xExpr = x; yExpr = y; zExpr = z; sMin = s0; sMax = s1; tMin = t0; tMax = t1; numFacetS = nfs; numFacetT = nft;
            colorString = colstr; name = nm;

            children = new List<ParamData>();
        }

        public ParamData(string nm, string colstr, string bcolstr, bool op, Matrix3D tm)
        {
            name = nm; colorString = colstr; backColorString = bcolstr; opaque = op; transformMatrix = tm;

            children = new List<ParamData>();
        }

        public ParamData(SerializationInfo si, StreamingContext sc)
        {
            name = si.GetString("name");
            xExpr = si.GetString("xExpr");
            yExpr = si.GetString("yExpr");
            zExpr = si.GetString("zExpr");
            sMin = si.GetString("sMin");
            sMax = si.GetString("sMax");
            tMin = si.GetString("tMin");
            tMax = si.GetString("tMax");
            numFacetS = si.GetInt32("numFacetS");
            numFacetT = si.GetInt32("numFacetT");
            xCenter = si.GetDouble("xCenter");
            yCenter = si.GetDouble("yCenter");
            zCenter = si.GetDouble("zCenter");
            colorString = si.GetString("colorString");
            backColorString = si.GetString("backColorString");
            opaque = si.GetBoolean("opaque");
            transformMatrix = (Matrix3D)si.GetValue("transformMatrix", typeof(Matrix3D));
            imageIndices = (int[])si.GetValue("imageIndices", typeof(int[]));
            imageTiled = si.GetBoolean("imageTiled");
            imageTransformMatrix = (Matrix)si.GetValue("imageTransformMatrix", typeof(Matrix));
            children = (List<ParamData>)si.GetValue("children", typeof(List<ParamData>));

            Byte[] imageArray = (Byte[])si.GetValue("imageArray", typeof(Byte[]));
            if (imageArray == null) imageSource = null;
            else
            {
                MemoryStream stream = new MemoryStream(imageArray);
                BmpBitmapDecoder decoder = new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                imageSource = decoder.Frames[0];
            }

            highlighted = false;
        }

        public virtual void GetObjectData(SerializationInfo si, StreamingContext sc)
        {
            si.AddValue("Version", 1);
            si.AddValue("name", name);
            si.AddValue("xExpr", xExpr);
            si.AddValue("yExpr", yExpr);
            si.AddValue("zExpr", zExpr);
            si.AddValue("sMin", sMin);
            si.AddValue("sMax", sMax);
            si.AddValue("tMin", tMin);
            si.AddValue("tMax", tMax);
            si.AddValue("numFacetS", numFacetS);
            si.AddValue("numFacetT", numFacetT);
            si.AddValue("xCenter", xCenter);
            si.AddValue("yCenter", yCenter);
            si.AddValue("zCenter", zCenter);
            si.AddValue("colorString", colorString);
            si.AddValue("backColorString", backColorString);
            si.AddValue("opaque", opaque);
            si.AddValue("transformMatrix", transformMatrix);
            si.AddValue("imageIndices", imageIndices);
            si.AddValue("imageTransformMatrix", imageTransformMatrix);
            si.AddValue("imageTiled", imageTiled);
            si.AddValue("children", children);

            Byte[] imageArray = null;
            if (imageSource != null)
            {
                MemoryStream stream = new MemoryStream();
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(imageSource));
                encoder.Save(stream);
                stream.Flush();
                imageArray = stream.ToArray();
            }
            si.AddValue("imageArray", imageArray);
        }


        public ParamData()
        {
            children = new List<ParamData>();
        }

        public virtual ParamData Clone()
        {
            ParamData pd = new ParamData(xExpr, yExpr, zExpr, sMin, sMax, tMin, tMax, numFacetS, numFacetT, colorString, name);
            pd.TransformMatrix = TransformMatrix;
            pd.BackColorString = backColorString;
            pd.Opaque = opaque;
            pd.ImageSource = imageSource;
            pd.ImageIndices = imageIndices;
            pd.ImageTiled = imageTiled;
            pd.ImageTransformMatrix = imageTransformMatrix;
            if (Children.Count > 0) foreach (ParamData p in Children) pd.Children.Add(p.Clone());
            return pd;
        }


        public Model3D GetModel3D()
        {
            Model3D m3D;
            if (Children.Count > 0)
            {
                m3D = new Model3DGroup();
                foreach (ParamData p in Children) ((Model3DGroup)m3D).Children.Add(p.GetModel3D());
                m3D.Transform = new MatrixTransform3D(TransformMatrix);
            }
            else m3D = GetGeometryModel3D();
            return m3D;
        }

        public GeometryModel3D GetGeometryModel3D()
        {
            MeshGeometry3D meshGeom = this.GetMeshGeometry3D();
            MaterialGroup materialGroup = new MaterialGroup();
            //PropertyInfo prop = typeof(Colors).GetProperty(this.colorString);
            //Color color = (Color)prop.GetValue(null, null);
            //prop = typeof(Colors).GetProperty(this.backColorString);
            //Color backColor = (Color)prop.GetValue(null, null);
            Color color = ParamModVis3D.ColorFromString(this.colorString);
            Color backColor = ParamModVis3D.ColorFromString(this.backColorString);
            DiffuseMaterial material = new DiffuseMaterial(new SolidColorBrush(color));
            DiffuseMaterial backMaterial = new DiffuseMaterial(new SolidColorBrush(backColor));
            if (!opaque)
            {
                ((SolidColorBrush)material.Brush).Opacity = 0.3;
                ((SolidColorBrush)backMaterial.Brush).Opacity = 0.3;
            }

            materialGroup.Children.Add(material);
            materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush()));
            materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 30));
            materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush()));

            GeometryModel3D geomMod3D = new GeometryModel3D(meshGeom, materialGroup);

            if (imageSource != null) SetModel3DImage(geomMod3D);

            MaterialGroup backMaterialGroup = new MaterialGroup();
            backMaterialGroup.Children.Add(backMaterial);
            backMaterialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 30));
            geomMod3D.BackMaterial = backMaterialGroup;
            geomMod3D.Transform = new MatrixTransform3D(TransformMatrix);
            return geomMod3D;
        }

        protected virtual MeshGeometry3D GetMeshGeometry3D()
        {
            int Ns = this.NumFacetS + 1;
            int Nt = this.NumFacetT + 1;

            ExpressionParser xExprParser = new ExpressionParser(this.XExpr, "s", "t");
            ExpressionParser yExprParser = new ExpressionParser(this.YExpr, "s", "t");
            ExpressionParser zExprParser = new ExpressionParser(this.ZExpr, "s", "t");

            double dSmin = (new ExpressionParser(this.SMin, null)).runnable(null);
            double dSmax = (new ExpressionParser(this.SMax, null)).runnable(null);
            double dTmin = (new ExpressionParser(this.TMin, null)).runnable(null);
            double dTmax = (new ExpressionParser(this.TMax, null)).runnable(null);


            Func<double, double, double> xFunc = (double s, double t) => xExprParser.runnable(new double[] { s, t });
            Func<double, double, double> yFunc = (double s, double t) => yExprParser.runnable(new double[] { s, t });
            Func<double, double, double> zFunc = (double s, double t) => zExprParser.runnable(new double[] { s, t });

            MeshGeometry3D meshGeom = new MeshGeometry3D();
            for (int i = 0; i < Ns; i++)
                for (int j = 0; j < Nt; j++)
                {
                    double s = dSmin + (double)i * (dSmax - dSmin) / (double)this.NumFacetS;
                    double t = dTmin + (double)j * (dTmax - dTmin) / (double)this.NumFacetT;
                    meshGeom.Positions.Add(new Point3D(xFunc(s, t), yFunc(s, t), zFunc(s, t)));
                }

            bool WrapS;
            bool WrapT;
            SetMeshGeometry3DWrapBehavior(meshGeom, Ns, Nt, out WrapS, out WrapT);

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

        protected void SetMeshGeometry3DWrapBehavior(MeshGeometry3D meshGeom, int Ns, int Nt, out bool WrapS, out bool WrapT)
        {
            WrapT = true;
            for (int i = 0; i < Ns; i++)
                if (Point3D.Subtract(meshGeom.Positions[GetPositionIndex(i, Nt - 1, Ns, Nt)], meshGeom.Positions[GetPositionIndex(i, 0, Ns, Nt)]).Length
                    > ParamModVis3D.WrapThreshhold)
                { WrapT = false; break; }
            WrapS = true;
            for (int j = 0; j<Nt; j++)
                if (Point3D.Subtract(meshGeom.Positions[GetPositionIndex(Ns - 1, j, Ns, Nt)], meshGeom.Positions[GetPositionIndex(0, j, Ns, Nt)]).Length
                    > ParamModVis3D.WrapThreshhold)
                { WrapS = false; break; }

            if (WrapT) 
                for (int i = 0; i < Ns; i++) meshGeom.Positions[GetPositionIndex(i, Nt - 1, Ns, Nt)] = meshGeom.Positions[GetPositionIndex(i, 0, Ns, Nt)];
            if (WrapS) 
                for (int j = 0; j < Nt; j++) meshGeom.Positions[GetPositionIndex(Ns - 1, j, Ns, Nt)] = meshGeom.Positions[GetPositionIndex(0, j, Ns, Nt)];

            return;
        }

        protected static void AddNormalsToMeshGeometry3D(MeshGeometry3D meshGeom, int Ns, int Nt, bool WrapS, bool WrapT)
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

        public void SetModel3DImage(Model3D mod3D)
        {
            if (!(mod3D is GeometryModel3D)) return;
            GeometryModel3D geomMod3D = (GeometryModel3D)mod3D;
            MeshGeometry3D meshGeom = (MeshGeometry3D)geomMod3D.Geometry;
            MaterialGroup materialGroup = (MaterialGroup)geomMod3D.Material;
            if (imageSource == null || imageIndices == null || imageIndices.Length<4)
            {
                materialGroup.Children[1] = new DiffuseMaterial(new SolidColorBrush());
                return;
            }
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
                        meshGeom.TextureCoordinates[ParamData.GetPositionIndex(i, j, Ns, Nt)] =
                            new Point((double)(i - iBegin) / (double)(iEnd - iBegin), (double)(j - jBegin) / (double)(jEnd - jBegin));
                }
            materialGroup.Children[1] = new DiffuseMaterial(imgBrush);
        }

    }

    [Serializable]
    class ParamExtrusionData : ParamData
    {
        string xPrimeExpr, yPrimeExpr;

        public string XPrimeExpr { get { return xPrimeExpr; } }
        public string YPrimeExpr { get { return yPrimeExpr; } }

        public ParamExtrusionData(string x, string y, string z, string xp, string yp, string s0, string s1, string t0,
            string t1, int nfs, int nft, string colstr, string nm) : base(x, y, z, s0, s1, t0, t1, nfs, nft, colstr, nm)
        {
            xPrimeExpr = xp; yPrimeExpr = yp; 
        }

        public ParamExtrusionData(SerializationInfo si, StreamingContext sc)
            : base(si, sc)
        {
            xPrimeExpr = si.GetString("xPrimeExpr");
            yPrimeExpr = si.GetString("yPrimeExpr");
        }

        public override void GetObjectData(SerializationInfo si, StreamingContext sc)
        {
            base.GetObjectData(si, sc);
            si.AddValue("xPrimeExpr", xPrimeExpr);
            si.AddValue("yPrimeExpr", yPrimeExpr);
        }

        public override ParamData Clone()
        {
            ParamData pd = new ParamExtrusionData(xExpr, yExpr, zExpr, xPrimeExpr, yPrimeExpr ,sMin, sMax, tMin, tMax, numFacetS, numFacetT, colorString, name);
            pd.TransformMatrix = TransformMatrix;
            pd.BackColorString = backColorString;
            pd.Opaque = opaque;
            pd.ImageSource = imageSource;
            pd.ImageIndices = imageIndices;
            pd.ImageTiled = imageTiled;
            pd.ImageTransformMatrix = imageTransformMatrix;
            if (Children.Count > 0) foreach (ParamData p in Children) pd.Children.Add(p.Clone());
            return pd;
        }

        protected override MeshGeometry3D GetMeshGeometry3D()
        {
            int Ns = this.NumFacetS + 1;
            int Nt = this.NumFacetT + 1;

            double dSmin = new ExpressionParser(this.SMin, null).runnable(null);
            double dSmax = new ExpressionParser(this.SMax, null).runnable(null);
            double dTmin = new ExpressionParser(this.TMin, null).runnable(null);
            double dTmax = new ExpressionParser(this.TMax, null).runnable(null);

            ExpressionParser xExpr = new ExpressionParser(this.XExpr, "s");
            ExpressionParser yExpr = new ExpressionParser(this.YExpr, "s");
            ExpressionParser zExpr = new ExpressionParser(this.ZExpr, "s");
            ExpressionParser xPrExpr = new ExpressionParser(this.XPrimeExpr, "s", "t");
            ExpressionParser yPrExpr = new ExpressionParser(this.YPrimeExpr, "s", "t");

            Func<double, double> xFunc = (double s) => xExpr.runnable(new double[] { s });
            Func<double, double> yFunc = (double s) => yExpr.runnable(new double[] { s });
            Func<double, double> zFunc = (double s) => zExpr.runnable(new double[] { s });
            Func<double, double, double> xPrFunc = (double s, double t) => xPrExpr.runnable(new double[] { s, t });
            Func<double, double, double> yPrFunc = (double s, double t) => yPrExpr.runnable(new double[] { s, t });

            MeshGeometry3D meshGeom = new MeshGeometry3D();

            int i, j;
            double ds = (dSmax - dSmin) / (double)this.NumFacetS;
            double dt = (dTmax - dTmin) / (double)this.NumFacetT;

            Point3D[] center = new Point3D[Ns];
            for (i = 0; i < Ns; i++)
            {
                double s = dSmin + (double)i * ds;
                center[i] = new Point3D(xFunc(s), yFunc(s), zFunc(s));
            }

            bool extrusionWrapS = Point3D.Subtract(center[0], center[Ns - 1]).Length < ParamModVis3D.WrapThreshhold;

            Vector3D[] dirVec = new Vector3D[Ns + 1];
            dirVec[0] = Point3D.Subtract(center[0], new Point3D(xFunc(dSmin - ds), yFunc(dSmin - ds), zFunc(dSmin - ds)));
            if (dirVec[0].Length < 0.0000001) throw new Exception("Error: Gradient of extrusion curve is zero");
            dirVec[0].Normalize();
            for (i = 1; i < Ns; i++)
            {
                dirVec[i] = Point3D.Subtract(center[i], center[i - 1]);
                if (dirVec[i].Length < 0.0000001) throw new Exception("Error: Gradient of extrusion curve is zero");
                dirVec[i].Normalize();
            }
            dirVec[Ns] = extrusionWrapS ? Point3D.Subtract(center[1], center[0]) :
                Point3D.Subtract(new Point3D(xFunc(dSmax + ds), yFunc(dSmax + ds), zFunc(dSmax + ds)), center[Ns - 1]);
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
                if (axisFoundIndex > 0) for (i = 0; i < axisFoundIndex; i++) xPrimeAxis[i] = xPrimeAxis[axisFoundIndex];
                int Navg = Ns / 10;
                Vector3D[] xPrimeAxisAvg = new Vector3D[Ns];
                Vector3D[] yPrimeAxisAvg = new Vector3D[Ns];
                for (i = 0; i < Ns; i++)
                {
                    int jMin = i - Navg;
                    int jMax = i + Navg;
                    if (!extrusionWrapS)
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
                    double s = dSmin + (double)i * ds;
                    double t = dTmin + (double)j * dt;
                    Vector3D xPrimeVector = Vector3D.Multiply(xPrFunc(s, t), xPrimeAxis[i]);
                    Vector3D yPrimeVector = Vector3D.Multiply(yPrFunc(s, t), yPrimeAxis[i]);
                    Point3D pos = Point3D.Add(center[i], Vector3D.Add(xPrimeVector, yPrimeVector));
                    meshGeom.Positions.Add(pos);
                }

            bool WrapS;
            bool WrapT;
            SetMeshGeometry3DWrapBehavior(meshGeom, Ns, Nt, out WrapS, out WrapT);

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
    }


    /*
    ParameterObjectData and ParameterExtrusionObjectData are legacy classes, necessary to open p3d files created with previous
    version of Parameter3D. They must be kept, because some of the shapes in the Shapes menu and Shapes toobar were created with
    the old version.
    */

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
        [OptionalField(VersionAdded = 2)]
        Byte[] imageArray = null;
        [OptionalField(VersionAdded = 2)]
        [NonSerialized]
        public BitmapSource imageSource = null;
        [OptionalField(VersionAdded = 2)]
        public int[] imageIndices = new int[] { 0, 0, 0, 0 };
        [OptionalField(VersionAdded = 2)]
        public string backColorString = "DarkGray";
        [OptionalField(VersionAdded = 2)]
        public bool translucent = false;
        [OptionalField(VersionAdded = 2)]
        public double xCenter = 0;
        [OptionalField(VersionAdded = 2)]
        public double yCenter = 0;
        [OptionalField(VersionAdded = 2)]
        public double zCenter = 0;
        [OptionalField(VersionAdded = 2)]
        public Matrix imageTransformMatrix = Matrix.Identity;
        [OptionalField(VersionAdded = 2)]
        public bool imageTiled = false;


        public ParameterObjectData()
        {
            transformMatrix = Matrix3D.Identity;
            Children = new List<ParameterObjectData>();
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
        protected void OnSerializing(StreamingContext sc)
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
        protected void OnSerialized(StreamingContext sc)
        {
            imageArray = null;
        }

        [OnDeserialized]
        protected void OnDeserialized(StreamingContext sc)
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

        /*
        public ParameterExtrusionObjectData(string xf, string yf, string zf, string xpf, string ypf,
            double smin, double smax, double tmin, double tmax, int nFacS, int nFacT, bool ws, bool wt, string cStr)
            : base(xf, yf, zf, smin, smax, tmin, tmax, nFacS, nFacT, ws, wt, cStr)
        {
            xPrimeFunction = xpf;
            yPrimeFunction = ypf;
        }
        

        public override MeshGeometry3D GetMeshGeometry3D()
        {
            int Ns = this.NumFacetS + 1;
            int Nt = this.NumFacetT + 1;

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
            dirVec[0] = WrapS ? Point3D.Subtract(center[Ns - 1], center[Ns - 2]) :
                Point3D.Subtract(center[0], new Point3D(xFunc(this.Smin - ds), yFunc(this.Smin - ds), zFunc(this.Smin - ds)));
            if (dirVec[0].Length < 0.0000001) throw new Exception("Error: Gradient of extrusion curve is zero");
            dirVec[0].Normalize();
            for (i = 1; i < Ns; i++)
            {
                dirVec[i] = Point3D.Subtract(center[i], center[i - 1]);
                if (dirVec[i].Length < 0.0000001) throw new Exception("Error: Gradient of extrusion curve is zero");
                dirVec[i].Normalize();
            }

            dirVec[Ns] = WrapS ? Point3D.Subtract(center[1], center[0]) :
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
                if (axisFoundIndex > 0) for (i = 0; i < axisFoundIndex; i++) xPrimeAxis[i] = xPrimeAxis[axisFoundIndex];
                int Navg = Ns / 10;
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
        */

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
                pd.ImageIndices = imageIndices;
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
            catch (Exception e)
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
            for (int i = 0; i < (ParamNames.Length + 2); i++)
                if (i < ParamNames.Length) extPNames2[i] = ParamNames[i];
                else if (i == ParamNames.Length) extPNames2[i] = "s";
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

        public void AddParamName(string newParamName)
        {
            string[] tempNames;
            if (ParamNames == null || ParamNames.Length == 0)
            {
                tempNames = new string[1] { newParamName };
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
            if (ParamNames == null || index >= ParamNames.Length || index < 0) return;
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
            string nFacS, string nFacT, bool ws, bool wt, params string[] pnames)
            : base(nm, descr, xf, yf, zf, smin, smax, tmin, tmax, nFacS, nFacT, ws, wt, pnames)
        {
            xPrimeExpr = xpf;
            yPrimeExpr = ypf;
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