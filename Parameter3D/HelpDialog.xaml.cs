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
using System.IO;
using System.Reflection;

namespace Parameter3D
{
    /// <summary>
    /// Interaction logic for HelpDialog.xaml
    /// </summary>
    public partial class HelpDialog : Window
    {
        public HelpDialog()
        {
            InitializeComponent();
            Assembly a = Assembly.GetExecutingAssembly();
            string loc = System.IO.Path.GetDirectoryName(a.Location);
            string instructionFileName = loc + "\\" + "Parameter3DInstructions.txt";
            using (StreamReader sr = new StreamReader(instructionFileName))
            {
                tbxHelp.Text = sr.ReadToEnd();
            }
        }
    }
}
