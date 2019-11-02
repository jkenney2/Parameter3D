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
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Microsoft.Win32;

namespace Parameter3D
{
    /// <summary>
    /// Interaction logic for CreateTemplatedialog.xaml
    /// </summary>
    public partial class CreateTemplatedialog : Window
    {
        ParameterObjectTemplate pObjTemplate;

        public CreateTemplatedialog()
        {
            InitializeComponent();
        }

        private void btnAddSurface_Click(object sender, RoutedEventArgs e)
        {
                TemplateSurface tSurf = new TemplateSurface(null, pObjTemplate.ParamNames);
                bool? result = tSurf.ShowDialog();
                if (result == true)
                {
                    pObjTemplate.Children.Add((tSurf.Tag as ParameterObjectTemplate));
                    lbxSurfaceList.Items.Add( (tSurf.Tag as ParameterObjectTemplate).name );
                }
        }

        private void btnEditSurface_Click(object sender, RoutedEventArgs e)
        {
            if (pObjTemplate.Children == null || pObjTemplate.Children.Count == 0) return;
            int index = lbxSurfaceList.SelectedIndex;
            if (index >= 0)
            {
                TemplateSurface tSurf = new TemplateSurface(pObjTemplate.Children[index], pObjTemplate.ParamNames);
                bool? result = tSurf.ShowDialog();
                if (result == true)
                {
                    pObjTemplate.Children[index] = tSurf.Tag as ParameterObjectTemplate;
                    lbxSurfaceList.Items.RemoveAt(index);
                    lbxSurfaceList.Items.Insert(index, (tSurf.Tag as ParameterObjectTemplate).name);
                }
            }
        }

        private void btnDeleteSurface_Click(object sender, RoutedEventArgs e)
        {
            if (pObjTemplate == null || pObjTemplate.Children.Count == 0) return;
            int index = lbxSurfaceList.SelectedIndex;
            if (index >= 0)
            {
                pObjTemplate.Children.RemoveAt(index);
                lbxSurfaceList.Items.RemoveAt(index);
            }
            return;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbxTemplateName.Text))
            {
                MessageBox.Show("A template name is required.");
                return;
            }

            pObjTemplate.name = tbxTemplateName.Text;
            pObjTemplate.description = tbxDescription.Text;

            if (!pObjTemplate.IsValidTemplate())
            {
                MessageBox.Show("Template is not valid; no template was saved.");
                return;
            }

            BinaryFormatter bf = null;
            FileStream outfile = null;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Parameter 3D Template files (.p3t)|*.p3t";
            dlg.DefaultExt = ".p3t";
            bool? result = dlg.ShowDialog();

            if (result == true)
            {
                try
                {
                    bf = new BinaryFormatter();
                    outfile = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
                    if (pObjTemplate.Children.Count == 1)
                    {
                        pObjTemplate.Children[0].description = tbxDescription.Text;
                        bf.Serialize(outfile, pObjTemplate.Children[0]);
                    }
                    else
                    {
                        pObjTemplate.description = tbxDescription.Text;
                        bf.Serialize(outfile, pObjTemplate);
                    }
                    MessageBox.Show("Template was saved.");
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Save Template File Exception: " + ex.ToString());
                }
                finally
                {
                    if (outfile != null) outfile.Close();
                }
            }
            else
            {
                MessageBox.Show("No template was saved.");
            }
            return;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            pObjTemplate = new ParameterObjectTemplate((string)null, (string[])null);
        }

        private void btnAddNames_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbxAddNames.Text))
            {
                MessageBox.Show("Add Names textbox is empty; no new names added.");
                return;
            }
            string[] newNames = tbxAddNames.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (newNames == null || newNames.Length == 0)
            {
                MessageBox.Show("Add Names textbox contains no names; no new names added.");
                return;
            }
            for (int i = 0; i < newNames.Length; i++)
            {
                if (ExpressionParser.IsValidName(newNames[i]))
                {
                    pObjTemplate.AddParamName(newNames[i]);
                    lbxParamNames.Items.Add(newNames[i]);
                }
                else MessageBox.Show(newNames[i] + " is not a valid name; this name is not added.");
            }
        }

        private void btnDeleteName_Click(object sender, RoutedEventArgs e)
        {
            if (lbxParamNames.Items == null || lbxParamNames.Items.Count == 0 || lbxParamNames.SelectedIndex < 0)
            {
                MessageBox.Show("No names selected for deletion.");
                return;
            }
            int index = lbxParamNames.SelectedIndex;
            lbxParamNames.Items.RemoveAt(index);
            pObjTemplate.DeleteParamName(index);
        }

        private void btnFromFile_Click(object sender, RoutedEventArgs e)
        {
            ParameterObjectTemplate tempTemplate;
            BinaryFormatter bf = null;
            FileStream infile = null;
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Parameter 3D Template files (.p3t)|*.p3t";
            dlg.DefaultExt = ".p3t";
            bool? result = dlg.ShowDialog();
            if (result != true)
            {
                MessageBox.Show("No template file was opened.");
                return;
            }

            try
            {
                bf = new BinaryFormatter();
                infile = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read);
                tempTemplate = (ParameterObjectTemplate)bf.Deserialize(infile);
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

            if (tempTemplate.Children == null || tempTemplate.Children.Count == 0)
            {
                pObjTemplate = new ParameterObjectTemplate(tempTemplate.name, tempTemplate.ParamNames);
                pObjTemplate.description = tempTemplate.description;
                pObjTemplate.Children.Add(tempTemplate);
            }
            else pObjTemplate = tempTemplate;

            tbxTemplateName.Clear();
            tbxDescription.Clear();

            if (!string.IsNullOrEmpty(pObjTemplate.name)) tbxTemplateName.Text = pObjTemplate.name;
            if (!string.IsNullOrEmpty(pObjTemplate.description)) tbxDescription.Text = pObjTemplate.description;
            lbxParamNames.Items.Clear();
            if (pObjTemplate.ParamNames != null && pObjTemplate.ParamNames.Length > 0)
                for (int i = 0; i < pObjTemplate.ParamNames.Length; i++) lbxParamNames.Items.Add(pObjTemplate.ParamNames[i]);

            tbxAddNames.Clear();

            lbxSurfaceList.Items.Clear();
            if (pObjTemplate.Children != null && pObjTemplate.Children.Count > 0)
                for (int j =0; j < pObjTemplate.Children.Count; j++) lbxSurfaceList.Items.Add(pObjTemplate.Children[j].name);

        }

    }
}
