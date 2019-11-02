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
using System.Windows.Media.Media3D;
using System.IO;
using Microsoft.Win32;

namespace Parameter3D
{
    /// <summary>
    /// Interaction logic for AddImageDialog.xaml
    /// </summary>
    public partial class AddImageDialog : Window
    {
        ParamModVis3D pmv3D;
        string fileName;
        int Ns, Nt;

        public AddImageDialog(ParamModVis3D p)
        {
            pmv3D = p;
            Ns = p.ParamDataContent.NumFacetS + 1;
            Nt = p.ParamDataContent.NumFacetT + 1;
            InitializeComponent();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            int iBegin, iEnd, jBegin, jEnd;
            try
            {
                if (string.IsNullOrEmpty(fileName)) throw( new Exception( "File Name Required" ));
                if (string.IsNullOrEmpty(tbxIBegin.Text) || string.IsNullOrEmpty(tbxIend.Text) || string.IsNullOrEmpty(tbxJBegin.Text)
                    || string.IsNullOrEmpty(tbxJend.Text) ) throw( new Exception(" Indices must all be entered "));
                iBegin = Int32.Parse(tbxIBegin.Text);
                iEnd = Int32.Parse(tbxIend.Text);
                jBegin = Int32.Parse(tbxJBegin.Text);
                jEnd = Int32.Parse(tbxJend.Text);
                if (iBegin>=iEnd || jBegin>=jEnd)  throw( new Exception( "Indices out of Range" ));
                BitmapImage imgSource = new BitmapImage(new Uri(fileName));
                pmv3D.ImageIndices = new int[4] { iBegin, jBegin, iEnd, jEnd };
                pmv3D.ImageSource = imgSource;
                DialogResult = true;
                return;
                
            }
            catch (Exception ex)
            {
                MessageBox.Show("Image not added: " + ex.ToString());
                pmv3D.ImageSource = null;
                return;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lblIRange.Content = "Indices (I) for s parameter range from 0 to " + (Ns-1).ToString();
            lblJRange.Content = "Indices (J) for t parameter range from 0 to " + (Nt-1).ToString();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image files (.jpg, .bmp, .gif)|*.jpg;*.bmp;*.gif";
            dlg.DefaultExt = ".jpg";
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                fileName = dlg.FileName;
                tbxFileName.Text = fileName;
            }
        }
    }
}
