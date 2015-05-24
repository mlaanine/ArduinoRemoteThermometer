using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Xml;                   // Contains XmlParser
using System.ComponentModel;        // Contains INotifyPropertyChanged
using System.Windows.Threading;     // Contains DispatcherTimer
using System.IO;                    // Contains StreamReader
using System.IO.Ports;              // Contains SerialPort

namespace ArduinoRemoteThermometer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Dictionary<string, string> weatherStations = new Dictionary<string, string>();
        Observation observation = new Observation();
        DispatcherTimer timer = new DispatcherTimer();
        SerialPort serialPort = new SerialPort();

        public MainWindow()
        {
            InitializeComponent();

            ReadStationsFile();     // Populate weatherStations Dictionary from an external user editable file
            selectStation.ItemsSource = weatherStations;
            selectStation.DisplayMemberPath = "Value";
            selectStation.SelectedValuePath = "Key";

            this.DataContext = observation;

            // Update temperature info every 1 minutes:
            timer.Interval = new TimeSpan(0,1,0);   
            timer.Start();
            timer.Tick += new EventHandler(timer_Elapsed);
        }

        private void selectStation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This, Refresh_Click and timer_Elapsed execute the same lines. But it's not possible to
            // bind them all to a WPF Command because the ComboBox does not support Commands. Hence, created 
            // the UpdateAll() function: 
            UpdateAll();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (selectStation.SelectedValue != null)
            {
                UpdateAll();
            }
        }

        private void timer_Elapsed(object sender, EventArgs e)
        {
            if (selectStation.SelectedValue != null)
            {
                UpdateAll();
            }
        }

        public void UpdateAll()
        {
            // If SelectedValue is an integer then it's an FMI Station ID.
            // Else it's a place name for Weather Underground. 
            int fmisid;
            if (int.TryParse((string)selectStation.SelectedValue, out fmisid))
            {
                ReadFmiUrl(GenerateFmiUrl());
            }
            else
            {
                ReadWundergroundUrl(GenerateWundergroundUrl());
            }
            WriteToArduino();
        }

        private void selectComPort_DropDownOpened(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            selectComPort.Items.Clear();
            foreach (string port in ports)
            {
                selectComPort.Items.Add(port);
            }
        }

        private void selectComPort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Broken logic here: this gets called every time the ComboBox is opened ( -> selectComPort.Items.Clear()).
            // Preferred behavior should be that this is only called when a different COM port is selected.
            // On the other hand it's not good to populate the ComboBox only on startup, in case the user doesn't yet have 
            // the device plugged in.
            if (serialPort.IsOpen)
            {
                serialPort.Close();
                connectCom.IsEnabled = true;
            }
        }

        private void connectCom_Click(object sender, RoutedEventArgs e)
        {
            if (selectComPort.SelectedItem != null)
            {
                serialPort.PortName = selectComPort.SelectedValue.ToString();
                serialPort.BaudRate = 115200;
                // The rest are defaults: 8,N,1
                try
                {
                    serialPort.Open();
                    connectCom.IsEnabled = false;
                }
                catch (System.IO.IOException ex)
                {
                    Console.WriteLine("Problem opening serial port: {0}", ex);
                }
            }
        }

        public string GenerateFmiUrl()
        {
            string apiKey = "b94dabcb-20bd-49cc-b41a-0b4fb504d843";
            string startTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:00Z");
            string parsedUrl = "http://data.fmi.fi/fmi-apikey/" + apiKey +
                "/wfs?request=getFeature&storedquery_id=fmi::observations::weather::timevaluepair&fmisid=" +
                selectStation.SelectedValue +
                "&parameters=temperature&starttime=" +
                startTime;
            Console.WriteLine(parsedUrl);
            return parsedUrl;
        }

        public void ReadFmiUrl(string url)
        {
            string time ="";
            string temperature = "";
            try
            {
                using (XmlReader xmlReader = XmlReader.Create(url))
                {
                    while (xmlReader.Read())
                    {
                        if (xmlReader.IsStartElement())
                        {
                            switch (xmlReader.Name)
                            {
                                case "wml2:time":
                                    time = xmlReader.ReadString();
                                    break;
                                case "wml2:value":
                                    temperature = xmlReader.ReadString();
                                    break;
                            }
                        }
                    }
                }
                observation.Time = DateTime.ParseExact(time, "yyyy-MM-ddTHH:mm:00Z", null);
                observation.Temperature = Double.Parse(temperature);
            }
            catch (System.FormatException ex)
            {
                Console.WriteLine("Could not parse XML: {0}", ex);
            }
            catch (System.Net.WebException ex)
            {
                Console.WriteLine("Bad API/HTTP request: {0}", ex);
            }
        }

        public string GenerateWundergroundUrl()
        {
            string apiKey = "02da318bbeaf01e9";
            string parsedUrl = "http://api.wunderground.com/api/" + apiKey + "/conditions//q/" +
                selectStation.SelectedValue + ".xml";
            Console.WriteLine(parsedUrl);
            return parsedUrl;
        }

        public void ReadWundergroundUrl(string url)
        {
            string time = "";
            string temperature = "";
            using (XmlReader xmlReader = XmlReader.Create(url))
            {
                while (xmlReader.Read())
                {
                    if (xmlReader.IsStartElement())
                    {
                        switch (xmlReader.Name)
                        {
                            case "observation_time_rfc822":
                                time = xmlReader.ReadString();
                                break;
                            case "temp_c":
                                temperature = xmlReader.ReadString();
                                break;
                        }
                    }
                }
            }
            observation.Time = DateTime.ParseExact(time, "ddd, dd MMM yyyy HH:mm:ss zz00", null);
            observation.Temperature = Double.Parse(temperature);
        }

        public void WriteToArduino()
        {
            if (serialPort.IsOpen == true)
            {
                // Arduino display range is 53 degrees, minimum -20, maximum +33.
                // Map this range to 0 - 255
                double temp = observation.Temperature;
                if (temp < -20.0) { temp = -20.0; }
                if (temp > 33.0) { temp = 33.0; }
                temp = 255.0 / 53 * (temp + 20.0);
                byte[] ctrl = { Convert.ToByte(temp) };     // SerialPort.Write wants an array, even if it's an array of one.
                Console.WriteLine("Arduino ctrl byte: 0x{0:X}", ctrl[0]);
                serialPort.Write(ctrl, 0, 1);
            }
        }

        public void ReadStationsFile()
        {
            string line;
            Console.WriteLine("Loading stations.txt...");
            try
            {
                StreamReader file = new StreamReader("stations.txt");
                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Length > 0) && (line.Substring(0, 2) != "//"))
                    {
                        int i = line.IndexOf(" ", 0);
                        weatherStations.Add(line.Substring(0, i), line.Substring(i + 1));
                    }
                }
                file.Close();
            }
            catch(IOException ex)
            {
                Console.WriteLine("File not found {0}", ex);
            }
        }
    }

    public class Observation : INotifyPropertyChanged
    {
        private DateTime time;
        public DateTime Time
        {
            get { return this.time; }
            set
            {
                if (this.time != value)
                {
                    this.time = value;
                    this.NotifyPropertyChanged("Time");
                }
            }
        }

        private double temperature;
        public double Temperature
        {
            get { return this.temperature; }
            set
            {
                if (this.temperature != value)
                {
                    this.temperature = value;
                    this.NotifyPropertyChanged("Temperature");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
    }

}
