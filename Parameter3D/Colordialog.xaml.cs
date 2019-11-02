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
    /// Interaction logic for Colordialog.xaml
    /// </summary>
    public partial class Colordialog : Window
    {
        string InitialColorString;
        ParamModVis3D pmv3D;

        public Colordialog(ParamModVis3D p)
        {
            pmv3D = p;
            InitialColorString = p.ColorString;
            InitializeComponent();
        }

        public static string ColorStringFromHSV(double H, double S, double V)
        {
            int R, G, B;
            double var_r, var_g, var_b;

            if (S == 0)                       //HSV from 0 to 1
            {
                R = (int)(V * 255.0);
                G = (int)(V * 255.0);
                B = (int)(V * 255.0);
            }
            else
            {
                double var_h = H * 6;
                if (var_h == 6) var_h = 0;      //H must be < 1
                int var_i = (int)var_h;
                double var_1 = V * (1 - S);
                double var_2 = V * (1 - S * (var_h - var_i));
                double var_3 = V * (1 - S * (1 - (var_h - var_i)));

                if (var_i == 0) { var_r = V; var_g = var_3; var_b = var_1; }
                else if (var_i == 1) { var_r = var_2; var_g = V; var_b = var_1; }
                else if (var_i == 2) { var_r = var_1; var_g = V; var_b = var_3; }
                else if (var_i == 3) { var_r = var_1; var_g = var_2; var_b = V; }
                else if (var_i == 4) { var_r = var_3; var_g = var_1; var_b = V; }
                else { var_r = V; var_g = var_1; var_b = var_2; }

                R = (int)(var_r * 255.0);                  //RGB results from 0 to 255
                G = (int)(var_g * 255.0);
                B = (int)(var_b * 255.0);
            }

            return "#" + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            return;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Color InitialColor = ParamModVis3D.ColorFromString(InitialColorString);
            btnColor.Background = new SolidColorBrush( InitialColor );
            double InitialRed = (double)InitialColor.R / 255.0;
            double InitialGreen = (double)InitialColor.G / 255.0;
            double InitialBlue = (double)InitialColor.B / 255.0;
            double InitialHue, InitialSaturation, InitialValue;
            GetHSVFromRGB(InitialRed, InitialGreen, InitialBlue, out InitialHue, out InitialSaturation, out InitialValue);
            sldHue.Value = InitialHue;
            sldSaturation.Value = InitialSaturation;
            sldValue.Value = InitialValue;
            sldHue.ValueChanged += SliderValueChanged;
            sldSaturation.ValueChanged += SliderValueChanged;
            sldValue.ValueChanged += SliderValueChanged;
        }

        private void SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            string colStr = ColorStringFromHSV(sldHue.Value, sldSaturation.Value, sldValue.Value);
            ((SolidColorBrush)btnColor.Background).Color = ParamModVis3D.ColorFromString( colStr );
            pmv3D.ColorString = colStr;
            
        }

        private void GetHSVFromRGB(double R, double G, double B, out double H, out double S, out double V)
        {
            double M = Math.Max(Math.Max(R, G), B);
            double m = Math.Min(Math.Min(R, G), B);
            double C = M - m;
            double HPrime;
            if (C == 0) HPrime = 0;
            else if (M == R)
            {
                double temp = ((G - B) / C) / 6.0;
                HPrime = (temp - Math.Truncate(temp)) * 6.0;
            }
            else if (M == G) HPrime = (B - R) / C + 2.0;
            else HPrime = (R - G) / C + 4;
            H = HPrime / 6.0;
            V = M;
            S = C == 0 ? 0 : C / V;
        }



        private void Window_Closed(object sender, EventArgs e)
        {
            if (DialogResult == false) pmv3D.ColorString = InitialColorString;
        }
    }
}
