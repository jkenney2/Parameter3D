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

namespace Parameter3D
{
    /// <summary>
    /// Interaction logic for TemplateSurface.xaml
    /// </summary>
    public partial class TemplateSurface : Window
    {
        ParameterObjectTemplate paramTemplate;
        string[] paramNames;

        public TemplateSurface(ParameterObjectTemplate pTemplate, string[] pNames)
        {
            InitializeComponent();
            paramTemplate = pTemplate;
            paramNames = pNames;
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

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (tbxSurfaceName.Text == null || tbxSurfaceName.Text.Length == 0)
            {
                MessageBox.Show("A surface name is required.  No surface is added.");
                return;
            }
            if (cbxExtrusion.IsChecked == true) paramTemplate = new ParameterExtrusionObjectTemplate(tbxSurfaceName.Text, null,
                tbxXFunction.Text, tbxYFunction.Text, tbxZFunction.Text, tbxXPrimeFunction.Text, tbxYPrimeFunction.Text, tbxSMin.Text,
                tbxSMax.Text, tbxTMin.Text, tbxTMax.Text, tbxGridSizeS.Text, tbxGridSizeT.Text, cbxWrapS.IsChecked == true, cbxWrapT.IsChecked == true, paramNames);
            else paramTemplate = new ParameterObjectTemplate(tbxSurfaceName.Text, null, tbxXFunction.Text, tbxYFunction.Text,
                tbxZFunction.Text, tbxSMin.Text, tbxSMax.Text, tbxTMin.Text, tbxTMax.Text, tbxGridSizeS.Text, tbxGridSizeT.Text,
                cbxWrapS.IsChecked == true, cbxWrapT.IsChecked == true, paramNames);

            Tag = paramTemplate;
            
            DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (paramTemplate == null) return;
            tbxSurfaceName.Text = paramTemplate.name;
            tbxXFunction.Text = paramTemplate.xExpr;
            tbxYFunction.Text = paramTemplate.yExpr;
            tbxZFunction.Text = paramTemplate.zExpr;
            tbxSMin.Text = paramTemplate.SminExpr;
            tbxSMax.Text = paramTemplate.SmaxExpr;
            tbxTMin.Text = paramTemplate.TminExpr;
            tbxTMax.Text = paramTemplate.TmaxExpr;
            tbxGridSizeS.Text = paramTemplate.nsExpr;
            tbxGridSizeT.Text = paramTemplate.ntExpr;
            cbxWrapS.IsChecked = paramTemplate.WrapS;
            cbxWrapT.IsChecked = paramTemplate.WrapT;

            if (paramTemplate is ParameterExtrusionObjectTemplate)
            {
                cbxExtrusion.IsChecked = true;
                tbxXPrimeFunction.Text = ((ParameterExtrusionObjectTemplate)paramTemplate).xPrimeExpr;
                tbxYPrimeFunction.Text = ((ParameterExtrusionObjectTemplate)paramTemplate).yPrimeExpr;
            }
        }
    }
}
