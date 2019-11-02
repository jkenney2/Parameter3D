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
using System.Windows.Shapes;
using System.Reflection;

namespace Parameter3D
{
    /// <summary>
    /// Interaction logic for ShapeDialog.xaml
    /// </summary>
    public partial class ShapeDialog : Window
    {
        string shapeName;

        public ShapeDialog(string Name)
        {
            InitializeComponent();
            shapeName = Name;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title = shapeName;
            lblDim2.Visibility = Visibility.Collapsed;
            tbxDimension2.Visibility = Visibility.Collapsed;
            lblDim3.Visibility = Visibility.Collapsed;
            tbxDimension3.Visibility = Visibility.Collapsed;
            tbxDimension1.Text = "1.0";
            tbxDimension2.Text = "1.0";
            tbxDimension3.Text = "1.0";

            PropertyInfo[] colorPropInfo = typeof(Colors).GetProperties();
            for (int i = 0; i < colorPropInfo.Length; i++) comboBoxColor.Items.Add(colorPropInfo[i].Name);
            comboBoxColor.SelectedItem = "Gold";

            if (shapeName == "Sphere")
            {
                lblDim1.Content = "Radius";
            }
            else if (shapeName == "Cylinder")
            {
                lblDim1.Content = "Radius";
                lblDim2.Visibility = Visibility.Visible;
                tbxDimension2.Visibility = Visibility.Visible;
                tbxDimension2.Text = "0.5";
                lblDim2.Content = "Height";
            }
            else if (shapeName == "Block")
            {
                lblDim1.Content = "Length";
                lblDim2.Visibility = Visibility.Visible;
                lblDim2.Content = "Width";
                tbxDimension2.Visibility = Visibility.Visible;
                lblDim3.Visibility = Visibility.Visible;
                lblDim3.Content = "Height";
                tbxDimension3.Visibility = Visibility.Visible;
            }
            else if (shapeName == "Donut")
            {
                lblDim1.Content = "Major Radius";
                lblDim2.Visibility = Visibility.Visible;
                lblDim2.Content = "Minor Radius";
                tbxDimension2.Visibility = Visibility.Visible;
                tbxDimension2.Text = "0.3";
            }
            else if (shapeName == "Cone")
            {
                lblDim1.Content = "Radius";
                tbxDimension1.Text = "0.5";
                lblDim2.Visibility = Visibility.Visible;
                lblDim2.Content = "Height";
                tbxDimension2.Visibility = Visibility.Visible;
                tbxDimension2.Text = "1.0";
            }
            else if (shapeName == "Pyramid")
            {
                lblDim1.Content = "Length";
                lblDim2.Visibility = Visibility.Visible;
                lblDim2.Content = "Width";
                tbxDimension2.Visibility = Visibility.Visible;
                lblDim3.Visibility = Visibility.Visible;
                lblDim3.Content = "Height";
                tbxDimension3.Visibility = Visibility.Visible;
            }
            else
            {
                DialogResult = false;
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            double dim1 = 0, dim2 = 0, dim3 = 0;
            ParameterObjectData pobj = null;
            string xf, yf, zf;

            string colStr = (string)comboBoxColor.SelectedItem;

            try
            {
                dim1 = double.Parse(tbxDimension1.Text);
                if (dim1<=0) throw new Exception("Invalid dimension 1");
                if (tbxDimension2.IsVisible) 
                {
                    dim2 = double.Parse(tbxDimension2.Text);
                    if (dim2<=0) throw new Exception("Invalid dimension 2");
                }
                if (tbxDimension3.IsVisible) 
                {
                    dim3 = double.Parse(tbxDimension3.Text);
                    if (dim3<=0) throw new Exception("Invalid dimension 3");
                }
           
            }
            catch (Exception except)
            {
                MessageBox.Show("Error parsing shape input: " + except.Message);
                Tag = null;
                DialogResult = false;
                return;
            }

            if (shapeName == "Sphere")
            {
                xf = dim1.ToString() + "*Cos(s)*Cos(t)";
                yf = dim1.ToString() + "*Sin(s)*Cos(t)";
                zf = dim1.ToString() + "*Sin(t)";
                pobj = new ParameterObjectData(xf, yf, zf, 0, 6.28, -1.57, 1.57, 20, 10, true, false, colStr);
            }
            else if (shapeName == "Cylinder")
            {
                pobj = new ParameterObjectData();
                xf = dim1.ToString() + "*Cos(s)";
                yf = dim1.ToString() + "*Sin(s)";
                zf = dim2.ToString() + "*t";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, 0, 6.28, -0.5, 0.5, 20, 10, true, false, colStr));
                xf = dim1.ToString() + "*t*Cos(s)";
                yf = dim1.ToString() + "*t*Sin(s)";
                zf = dim2.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, 0, 6.28, 1, 0, 20, 5 ,true, false, colStr));
                zf = "-" + dim2.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, 0, 6.28, 0, 1, 20, 5, true, false, colStr));
            }
            else if (shapeName == "Block")
            {
                pobj = new ParameterObjectData();
                xf = dim1.ToString() + "*s";
                yf = dim2.ToString() + "*t";
                zf = dim3.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf,yf,zf,-0.5,0.5,-0.5,0.5,10,10,false,false,colStr));
                zf="-" + dim3.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf,yf,zf,-0.5,0.5,0.5,-0.5,10,10,false,false,colStr));
                xf = dim1.ToString() + "*0.5";
                yf = dim2.ToString() + "*s";
                zf = dim3.ToString() + "*t";
                pobj.Children.Add(new ParameterObjectData(xf,yf,zf,-0.5,0.5,-0.5,0.5,10,10,false,false,colStr));
                xf = "-" + dim1.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, 0.5, -0.5, 10, 10, false, false, colStr));
                xf = dim1.ToString() + "*t";
                yf = dim2.ToString() + "*0.5";
                zf = dim3.ToString() + "*s";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, -0.5, 0.5, 10, 10, false, false, colStr));
                yf = "-" + dim2.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, 0.5, -0.5, 10, 10, false, false, colStr));
            }
            else if (shapeName == "Donut")
            {
                xf = "Cos(s) * (" + dim1.ToString() + "+" + dim2.ToString() + "*Cos(t))";
                yf = "Sin(s) * (" + dim1.ToString() + "+" + dim2.ToString() + "*Cos(t))";
                zf = dim2.ToString() + "*Sin(t)";
                pobj = new ParameterObjectData(xf, yf, zf, 0, 6.28, 0, 6.28, 20, 10, true, true, colStr);
            }
            else if (shapeName == "Cone")
            {
                pobj = new ParameterObjectData();
                xf = dim1.ToString() + "* Cos(s) * (0.5 - t)";
                yf = dim1.ToString() + "* Sin(s) * (0.5 - t)";
                zf = dim2.ToString() + "* t";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, 0, 6.28, -0.5, 0.5, 20, 10, true, false, colStr));
                xf = dim1.ToString() + "*t*Cos(s)";
                yf = dim1.ToString() + "*t*Sin(s)";
                zf = "-" + dim2.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, 0, 6.28, 0, 1, 20, 5, true, false, colStr));
            }
            else if (shapeName == "Pyramid")
            {
                pobj = new ParameterObjectData();
                xf = dim1.ToString() + "*s";
                yf = dim2.ToString() + "*t";
                zf = "-" + dim3.ToString() + "*0.5";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, 0.5, -0.5, 10, 10, false, false, colStr));
                xf = dim1.ToString() + "* s * (0.5 - t)";
                yf = "0.5 * " + dim2.ToString() + " * (t-0.5)";
                zf = dim3.ToString() + "* t";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, -0.5, 0.5, 5, 5, false, false, colStr));
                yf = "-0.5 * " + dim2.ToString() + " * (t-0.5)";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, 0.5, -0.5, 5, 5, false, false, colStr));
                xf = "0.5 * " + dim1.ToString() + " * (t - 0.5)";
                yf = dim2.ToString() + "* s * (0.5 - t)";
                zf = dim3.ToString() + " * t";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, 0.5, -0.5, 5, 5, false, false, colStr));
                xf = "-0.5 * " + dim1.ToString() + " * (t - 0.5)";
                pobj.Children.Add(new ParameterObjectData(xf, yf, zf, -0.5, 0.5, -0.5, 0.5, 5, 5, false, false, colStr));
            }
            else
            {
                Tag = null;
                DialogResult = false;
                return;
            }

            Tag = pobj;
            DialogResult = true;
        }
    }
}
