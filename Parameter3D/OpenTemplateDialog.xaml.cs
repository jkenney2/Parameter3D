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
    /// Interaction logic for OpenTemplateDialog.xaml
    /// </summary>
    public partial class OpenTemplateDialog : Window
    {
        ParameterObjectTemplate paramObjTemplate;
        TextBox[] tbxsParamValues;

        public OpenTemplateDialog(ParameterObjectTemplate pObjTemp)
        {
            InitializeComponent();
            paramObjTemplate = pObjTemp;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            string[] paramValues = new string[paramObjTemplate.ParamNames.Length];
            ParamData pd;

            for (int i = 0; i < paramObjTemplate.ParamNames.Length; i++)
            {
                string nextParamValue = tbxsParamValues[i].Text;
                if (nextParamValue == null || nextParamValue.Length == 0)
                {
                    MessageBox.Show("Null or Zero-length parameter value");
                    return;
                }
                paramValues[i] = nextParamValue;
            }

            try
            {
                string colStr = (string)cbxColor.SelectedItem;
                pd = paramObjTemplate.GetParamData(colStr, paramValues);
                if (pd == null) return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception creating ParameterObjectData from ParameterObjectTemplate" + ex);
                return;
            }

            Tag = pd;
            DialogResult = true;
            return;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (paramObjTemplate == null || paramObjTemplate.ParamNames.Length == 0)
            {
                DialogResult = false;
                return;
            }
            tbxName.Text = paramObjTemplate.name;
            tbxDescription.Text = paramObjTemplate.description;


            PropertyInfo[] colorPropInfo = typeof(Colors).GetProperties();
            for (int i = 0; i < colorPropInfo.Length; i++) cbxColor.Items.Add(colorPropInfo[i].Name);
            cbxColor.SelectedItem = "Gold";

            tbxsParamValues = new TextBox[paramObjTemplate.ParamNames.Length];

            for (int i=0; i<paramObjTemplate.ParamNames.Length; i++)
            {
                grdParameters.RowDefinitions.Add(new RowDefinition());
                grdParameters.RowDefinitions[i].Height = GridLength.Auto;
                Label nextLabel = new Label();;
                nextLabel.Content = paramObjTemplate.ParamNames[i];
                grdParameters.Children.Add(nextLabel);
                Grid.SetRow(nextLabel, i);
                Grid.SetColumn(nextLabel, 0);
                tbxsParamValues[i] = new TextBox();
                tbxsParamValues[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                grdParameters.Children.Add(tbxsParamValues[i]);
                Grid.SetRow(tbxsParamValues[i],i);
                Grid.SetColumn(tbxsParamValues[i],1);
            }
            return;
        }
    }
}
