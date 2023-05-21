using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Python.Runtime;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScottPlot.Plottable;
using System.Drawing;
using System.Windows.Interop;
using Path = System.IO.Path;

namespace Potentials
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Запуск на весь экран по умолчанию 
            //Width = 1920;
            //Height = 1080;
            this.WindowState = WindowState.Maximized;

            // добавление легенды на графики
            One_AP_Plot.Plot.Legend();
            All_AP_Plot.Plot.Legend();

            RdV0_Plot.Plot.Legend();
            RdV4_Plot.Plot.Legend();
            dR_Plot.Plot.Legend();

            // добавление названия графика и подписей осей
            One_AP_Plot.Plot.Title("Cardiac Action Potential\n(SA node)");
            One_AP_Plot.Plot.XLabel("Time (mm:ss:ff)");
            One_AP_Plot.Plot.YLabel("Membrane Potential (mV)");
            One_AP_Plot.Plot.SetAxisLimitsY(-100, 40);

            All_AP_Plot.Plot.Title("Cardiac Action Potential\n(SA node)");
            All_AP_Plot.Plot.XLabel("Time (mm:ss:ff)");
            All_AP_Plot.Plot.YLabel("Membrane Potential (mV)");
            All_AP_Plot.Plot.SetAxisLimitsY(-100, 40);


            RdV0_Plot.Plot.Title("(dV/dt)0");
            RdV0_Plot.Plot.XLabel("Action Potential number");
            RdV0_Plot.Plot.YLabel("Rate of Change (V/s)");

            RdV4_Plot.Plot.Title("(dV/dt)4");
            RdV4_Plot.Plot.XLabel("Action Potential number");
            RdV4_Plot.Plot.YLabel("Rate of Change (mV/s)");

            dR_Plot.Plot.Title("Rate of change R");
            dR_Plot.Plot.XLabel("Action Potential number");
            dR_Plot.Plot.YLabel("Radius of curvature");

            // Прочий визуал, который подгружается по умолчанию
            One_AP_Plot.Plot.YAxis.Color(System.Drawing.Color.Blue);
            One_AP_Plot.Plot.YAxis2.Label("Rate of Change (V/s)");
            One_AP_Plot.Plot.YAxis2.Color(System.Drawing.Color.Red);
            One_AP_Plot.Plot.YAxis2.Ticks(true);

            All_AP_Plot.Plot.YAxis.Color(System.Drawing.Color.Blue);
            RdV0_Plot.Plot.YAxis.Color(System.Drawing.Color.Red);
            RdV4_Plot.Plot.YAxis.Color(System.Drawing.Color.Red);
        }

        // Объявление переменных
        string raw_filepath = "c:\\";
        string pythonExePath = "C:/Python39/python.exe";
        string separating_path = "";
        string ComboPath_with_params = "";
        string savedFilePath = "";
        string bebra = "";
        string raw_time = "";

        double[][] intervals;
        double[] phase_0_speed_array;
        double[] phase_4_speed_array;
        double[] num_of_APs;
        double[] radius_array;

        int time_ms = 0;
        int window_size = 3;

        string targetFile = "";
        double start_time = 0;
        double end_time = 0;

        double current_radius = 0;
        double phase_0_speed = 0;
        double phase_4_speed = 0;

        double alpha_threshold = 0.9;
        int start_offset = 0;
        int refractory_period = 280;

        bool check = false;
        bool Params = false;

        // выбрать файлик
        private async void OpenfileBtn_Click(object sender, RoutedEventArgs e)
        {
            // создание диалогового окна выбора файла
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // настройка параметров диалогового окна
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (!check)
            {
                openFileDialog.InitialDirectory = raw_filepath;
            }
            else
            {
                try
                {
                    openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(raw_filepath);
                }
                catch (Exception)
                {
                    openFileDialog.InitialDirectory = "c:\\";
                }
            }
            

            //openFileDialog.InitialDirectory = "c:\\";
            //openFileDialog.InitialDirectory = "D:\\programming\\projects\\py\\potentials\\";

            // Проверим, не сломал ли пользователь параметры
            bool Params = double.TryParse(alpha_threshold_TextBox.Text.Trim().Replace('.', ','), out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), out refractory_period);

            if (Params)
            {
                alpha_threshold = Math.Round(alpha_threshold, 3);

                // отображение диалогового окна
                if (openFileDialog.ShowDialog() == true)
                {
                    raw_filepath = openFileDialog.FileName;
                    //raw_filepath = RenameRussianToLatin(raw_filepath);

                    // Блокируем все от шаловливых ручек пользователя
                    Window_Block_All();

                    // Добро пожаловать в питон
                    await RunLongOperationAsync();

                    // сохранение пути к выбранному файлу в переменной raw_filepath и "/" заменяем "\" на "/"
                    FileNameTextBox.Text = System.IO.Path.GetFileName(raw_filepath); // извлекаем имя файла;

                    // Строим все графики, кроме одиночного ПД
                    await PlotAllAsync();

                    // Разблокируем кнопочки
                    SaveAP_Btn.IsEnabled = true;
                    FindAPBtn.IsEnabled = true;
                    FindAP_by_number_Btn.IsEnabled = true;
                    UpdateBtn.IsEnabled = true;

                    // Кнопка устарела
                    //PlotAllBtn.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show("Check next inputs:\nalpha-threshold\nstart-offset\nrefractory-period", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // !!СЕЙЧАС НЕ ИСПОЛЬЗУЕТСЯ, ТРЕБУЕТ ДОРАБОТКИ!! На случай, если нам подкинули руске в пути или файле 
        public static string RenameRussianToLatin(string path)
        {
            string map = "абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
            string mapTo = "abvgdeejzijklmnoprstufhcchshsh'i'euaABVGDEEJZIJKLMNOPRSTUFHCCHSHSH'I'EUA";
            char[] invalidChars = new char[] { '<', '>', '"', '/', '\\', '|', '?', '*', '\'', ' ', '-', ',' };

            string newPath = "";

            string[] directories = path.Split(Path.DirectorySeparatorChar);
            for (int i = 0; i < directories.Length; i++)
            {
                string newDirName = "";
                foreach (char c in directories[i])
                {
                    if (invalidChars.Contains(c))
                    {
                        newDirName += "_";
                        continue;
                    }

                    int index = map.IndexOf(c);
                    if (index != -1)
                    {
                        newDirName += mapTo[index];
                    }
                    else
                    {
                        newDirName += c;
                    }
                }

                directories[i] = newDirName;
                newPath = Path.Combine(newPath, newDirName);

                if (i < directories.Length - 1 && !Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                }
            }

            if (File.Exists(path))
            {
                File.Move(path, newPath);
            }
            else if (Directory.Exists(path))
            {
                Directory.Move(path, newPath);
            }

            return newPath;
        }



        // Будем распараллеливать открытие большого файла
        private async Task RunLongOperationAsync()
        {
            await Task.Run(() =>
            {
                // Здесь выполняется самая длительная операция
                string raw_filepath_with_params = raw_filepath + "\n" + alpha_threshold.ToString() + "\n" + start_offset.ToString() + "\n" + refractory_period.ToString();
                string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Save_APs_to_C#.py");
                Tuple<string, double[][], double[], double[], double[], double[]> pythonData = Backend.RunPythonScript_AllPD(pythonExePath, scriptPath, raw_filepath_with_params);
                separating_path = pythonData.Item1;
                intervals = pythonData.Item2;
                phase_0_speed_array = pythonData.Item3;
                phase_4_speed_array = pythonData.Item4;
                num_of_APs = pythonData.Item5;
                radius_array = pythonData.Item6;

                check = true;
            });
        }

        

        private async void PlotAllBtn_Click(object sender, RoutedEventArgs e)
        {
            // для построения графика в UpdateBtn вся логика вынесена в отдельную функцию
            await PlotAllAsync();
        }

        // Строим все графики, кроме одиночного ПД
        private async Task PlotAllAsync()
        {
            // проверка на window size
            string check_window_sise = window_size_TextBox.Text.Trim();

            if (!int.TryParse(check_window_sise, out int window_size) | !(window_size <= num_of_APs.Length - 1))
            {
                MessageBox.Show("Decrease the value of \"window_size\" or set it correctly");
            }
            else
            {
                // Очистка графика перед построением
                All_AP_Plot.Plot.Clear();

                // Предварительно Нацеливаемся на 1 первый файл (хоть 1 то должен быть)
                string targetFile = separating_path + "/1.txt";

                // От борьбы 2 потоков за одни и те же данные
                FindAPBtn.IsEnabled = false;

                var fileTasks = Directory.EnumerateFiles(separating_path)
                    .Where(filePath => System.IO.Path.GetExtension(filePath) == ".txt")
                    .Select(ProcessFileAsync);

                await Task.WhenAll(fileTasks);

                // Возвращаем свободу дейсвий
                FindAPBtn.IsEnabled = true;

                // Настройка пределов осей для отображения только первых 500 точек
                All_AP_Plot.Plot.SetAxisLimits(0, 2500, double.NaN, double.NaN);

                // Добавление пунктирных линий, соответствующих значениям из массива intervals
                for (int i = 0; i < intervals.Length; i++)
                {
                    for (int j = 0; j < intervals[i].Length; j++)
                    {
                        double xValue = intervals[i][j];
                        All_AP_Plot.Plot.AddVerticalLine(xValue, color: System.Drawing.Color.Gray, style: LineStyle.Dash);
                    }
                }

                All_AP_Plot.Plot.SetAxisLimitsY(-100, 40);

                All_AP_Plot.Plot.Benchmark(enable: true);

                // Обновление графика
                All_AP_Plot.Refresh();

                // находим длительность эксперимента в мс
                double[] lastInterval = intervals[intervals.Length - 1];
                double lastValue = lastInterval[lastInterval.Length - 1];

                // Переводим миллисекунды в минуты, секунды и доли секунды
                int minutes = (int)(lastValue / 60000);
                int seconds = (int)((lastValue % 60000) / 1000);
                int fractionalSeconds = (int)(lastValue % 1000);

                // Форматируем значения в нужном формате
                string formattedDuration = string.Format("{0:D2}:{1:D2}:{2:D3}", minutes, seconds, fractionalSeconds);

                // Выводим сообщение с отформатированным значением
                Experiment_duration_label.Text = "Experiment duration: " + formattedDuration;

                RdV0_Plot.Plot.Clear();
                RdV4_Plot.Plot.Clear();
                dR_Plot.Plot.Clear();

                // фокусы для доверительных интервалов -------------------------------------------------------
                var dV0 = Backend.ConfidenceIntervals_same(num_of_APs, phase_0_speed_array, window_size);
                double[] num_of_APs_window_size = dV0.Item1;
                double[] means_dV0 = dV0.Item2;
                double[] confidenceInterval_dV0 = dV0.Item3;

                var dV4 = Backend.ConfidenceIntervals_same(num_of_APs, phase_4_speed_array, window_size);
                double[] means_dV4 = dV4.Item2;
                double[] confidenceInterval_dV4 = dV4.Item3;

                var dR = Backend.ConfidenceIntervals_same(num_of_APs, radius_array, window_size);
                double[] means_dR = dR.Item2;
                double[] confidenceInterval_dR = dR.Item3;

                // используем наш custom formatter для формата времени под mm:ss:ff на большом графике
                All_AP_Plot.Plot.XAxis.TickLabelFormat(Backend.customTickFormatter);

                // Строим графики с доверительными интервалами 
                RdV0_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV0, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineStyle: LineStyle.Dot);
                RdV0_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV0, null, confidenceInterval_dV0);

                RdV4_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV4, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineStyle: LineStyle.Dot);
                RdV4_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV4, null, confidenceInterval_dV4);

                dR_Plot.Plot.AddScatter(num_of_APs_window_size, means_dR, lineStyle: LineStyle.Dot);
                dR_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dR, null, confidenceInterval_dR);

                RdV0_Plot.Refresh();
                RdV4_Plot.Refresh();
                dR_Plot.Refresh();
            }
        }

        private async Task ProcessFileAsync(string filePath)
        {
            var data = await Task.Run(() => Backend.ReadFileData(filePath));

            // Построение графика
            All_AP_Plot.Plot.AddScatterLines(data.Times.ToArray(), data.Potentials.ToArray(), lineWidth: 5, color: System.Drawing.Color.Black);
        }

        // найти по заданному времени участок и построить графики
        private void FindAPBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Чтобы 2 потока не боролись за одни и те же данные
                PlotAllBtn.IsEnabled = false;

                // Строим график для 1 ПД
                raw_time = TimeTextBox.Text.Trim();

                Backend.ParseTime(raw_time, out int minutes, out int seconds, out int fraction_seconds);

                time_ms = (minutes * 60 * 1000) + (seconds * 1000) + fraction_seconds;

                var result = Backend.FindFileForTime(separating_path, time_ms);
                targetFile = result.Item1;
                start_time = result.Item2;
                end_time = result.Item3;

                if (!string.IsNullOrEmpty(targetFile))
                {
                    Plot_And_Refresh();

                    // Код, взаимодействующий с Объектами
                    Next_AP_Btn.IsEnabled = true;
                    Previos_AP_Btn.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }


                // Возвращаем возможность строить остальное
                PlotAllBtn.IsEnabled = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Возвращаем возможность строить остальное
                PlotAllBtn.IsEnabled = true;
            }
        }

        private void FindAP_by_number_Btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Чтобы 2 потока не боролись за одни и те же данные
                PlotAllBtn.IsEnabled = false;

                // Строим график для 1 ПД
                raw_time = TimeTextBox.Text.Trim();

                if (int.TryParse(NumberAP_TextBox.Text.Trim(), out int numberAP))
                {
                    var result = Backend.FindFileForNumber(separating_path, numberAP);
                    targetFile = result.Item1;
                    start_time = result.Item2;
                    end_time = result.Item3;

                    if (!string.IsNullOrEmpty(targetFile) & File.Exists(targetFile))
                    {
                        Plot_And_Refresh();

                        // Код, взаимодействующий с Объектами
                        Next_AP_Btn.IsEnabled = true;
                        Previos_AP_Btn.IsEnabled = true;
                    }
                    else
                    {
                        MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Возвращаем возможность строить остальное
                PlotAllBtn.IsEnabled = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Возвращаем возможность строить остальное
                PlotAllBtn.IsEnabled = true;
            }
        }
        

        // общий метод построения графиков в маленьком окошке и смещения красных прямых
        void Plot_And_Refresh()
        {
            // Проверим, не сломал ли пользователь параметры
            Params = double.TryParse(alpha_threshold_TextBox.Text.Trim().Replace('.', ','), out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), out refractory_period);

            string targetFile_with_params = targetFile + "\n" + alpha_threshold.ToString() + "\n" + start_offset.ToString() + "\n" + refractory_period.ToString(); ;
            if (Params)
            {
                alpha_threshold = Math.Round(alpha_threshold, 3);

                double[] time, voltage;
                if (Backend.ParseFileData(targetFile, out time, out voltage))
                {
                    // Очистка графика перед построением
                    One_AP_Plot.Plot.Clear();

                    // Убираем красные хуйни
                    RemoveRedVerticalLines();

                    string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Circle_to_C#.py");
                    // Достаем данные для круга и скоростей
                    Tuple<double, double, double, double, double> CircleData = Backend.RunPythonScriptCircle(pythonExePath, scriptPath, targetFile_with_params);
                    current_radius = CircleData.Item1;
                    double x = CircleData.Item2;
                    double y = CircleData.Item3;
                    phase_0_speed = CircleData.Item4;
                    phase_4_speed = CircleData.Item5;

                    // Строим заготовку
                    One_AP_Plot.Plot.AddScatterLines(time, voltage, lineWidth: 5, label: $"R = {current_radius}");

                    // calculate the first derivative

                    double[] deriv = new double[voltage.Length];
                    //for (int i = 1; i < deriv.Length; i++)
                    //    deriv[i] = (voltage[i] - voltage[i - 1]) * time[i];
                    //deriv[0] = deriv[1];

                    deriv = Backend.CalculateDerivative(voltage, time);

                    // plot the first derivative in red on the secondary Y axis
                    var dVdt = One_AP_Plot.Plot.AddScatterLines(time, deriv, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), label: $"(dV/dt)0 =  {phase_0_speed} V/s\r\n(dV/dt)4 = {phase_4_speed} mV/s");
                    dVdt.YAxisIndex = 1;
                    dVdt.LineWidth = 3;
                    var legend = One_AP_Plot.Plot.Legend(enable: true);
                    legend.Orientation = ScottPlot.Orientation.Horizontal;


                    // Добавляем красные хуйни
                    All_AP_Plot.Plot.AddVerticalLine(start_time, color: System.Drawing.Color.Red, style: LineStyle.Solid);
                    All_AP_Plot.Plot.AddVerticalLine(end_time, color: System.Drawing.Color.Red, style: LineStyle.Solid);

                    // Добавляем на график круг
                    One_AP_Plot.Plot.AddCircle(x, y, current_radius, color: System.Drawing.Color.Red);

                    // добавление горизорнтальной полоски Овершут
                    One_AP_Plot.Plot.AddHorizontalLine(0, color: System.Drawing.Color.Gray, style: LineStyle.Dash);
                    //All_AP_Plot.Plot.AddHorizontalLine(0, color: System.Drawing.Color.Gray, style: LineStyle.Dash);

                    One_AP_Plot.Plot.SetAxisLimitsY(-90, 40);

                    // Переход на большом графике к интересному месту
                    All_AP_Plot.Plot.SetAxisLimitsX(start_time - 1000, end_time + 1000);
                    All_AP_Plot.Plot.SetAxisLimitsY(-100, 30);

                    // используем наш custom formatter для формата времени под mm:ss:ff на маленьком графике
                    One_AP_Plot.Plot.XAxis.TickLabelFormat(Backend.customTickFormatter);


                    One_AP_Plot.Plot.Benchmark(enable: true);
                    All_AP_Plot.Plot.Benchmark(enable: true);

                    One_AP_Plot.Refresh();
                    All_AP_Plot.Refresh();

                    // Устарело
                    // вывод радиуса и скоростей в лейблы
                    //RdМ_lbl.Visibility = Visibility.Visible;
                    //RdV_num_lbl.Visibility = Visibility.Visible;

                    //RdV_num_lbl.Content = $"= {current_radius}\r\n= {phase_0_speed}\r\n= {phase_4_speed}";
                }
            }
            else
            {
                MessageBox.Show("Check next inputs:\nalpha-threshold\nstart-offset\nrefractory-period", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // убрать все красные VLines
        private void RemoveRedVerticalLines()
        {
            var redVerticalLines = All_AP_Plot.Plot.GetPlottables()
                                    .OfType<VLine>()
                                    .Where(vl => vl.Color == System.Drawing.Color.Red)
                                    .ToList();

            foreach (var redLine in redVerticalLines)
            {
                All_AP_Plot.Plot.Remove(redLine);
            }
        }

        

        private void Next_AP_Btn_Click(object sender, RoutedEventArgs e)
        {
            int currentNumber = int.Parse(System.IO.Path.GetFileNameWithoutExtension(targetFile));
            int newNumber = currentNumber + 1;
            string newFileName = newNumber.ToString() + ".txt";

            if (File.Exists(System.IO.Path.Combine(separating_path, newFileName)))
            {
                targetFile = System.IO.Path.Combine(separating_path, newFileName);

                var result = Backend.GetMinMaxTime(targetFile);
                start_time = result.Item1;
                end_time = result.Item2;

                Plot_And_Refresh();
            }
            else
            {
                MessageBox.Show("Смещение вправо невозможно.");
            }
        }

        private void Previos_AP_Btn_Click(object sender, RoutedEventArgs e)
        {
            int currentNumber = int.Parse(System.IO.Path.GetFileNameWithoutExtension(targetFile));
            int newNumber = currentNumber - 1;
            string newFileName = newNumber.ToString() + ".txt";

            if (File.Exists(System.IO.Path.Combine(separating_path, newFileName)))
            {
                
                targetFile = System.IO.Path.Combine(separating_path, newFileName);

                var result = Backend.GetMinMaxTime(targetFile);
                start_time = result.Item1;
                end_time = result.Item2;

                Plot_And_Refresh();
            }
            else
            {
                MessageBox.Show("Смещение влево невозможно.");
            }
        }
        

        private async void SaveAP_Btn_Click(object sender, RoutedEventArgs e)
        {
            savedFilePath = Backend.SaveFileAndGetPath();
            string ComboPath = savedFilePath + "\n" + raw_filepath;

            // Работу с TextBox закинем в основной поток от греха подальше
            await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Проверим, не сломал ли пользователь параметры
                Params = double.TryParse(alpha_threshold_TextBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out refractory_period);

            }));

            ComboPath_with_params = ComboPath + "\n" + alpha_threshold.ToString() + "\n" + start_offset.ToString() + "\n" + refractory_period.ToString(); ;

            if (Params)
            {
                alpha_threshold = Math.Round(alpha_threshold, 3);

                if (!string.IsNullOrEmpty(savedFilePath))
                {
                    // Лезем в питон сохранять все в табличку
                    await RunLongSaveAsync();
                    if (bebra == "true") { MessageBox.Show($"File saved successfully to\r\n{savedFilePath}"); }
                }
                else
                {
                    MessageBox.Show("The save operation was canceled.");
                }
            }
            else
            {
                MessageBox.Show("Check next inputs:\nalpha-threshold\nstart-offset\nrefractory-period", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        

        // Будем распараллеливать сохранение данных большого файла
        private async Task RunLongSaveAsync()
        {
            await Task.Run(() =>
            {
                // Здесь выполняется сохранение в табличку
                string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Table_to_C#.py");
                bebra = Backend.RunPythonScript_Save_xlxs(pythonExePath, scriptPath, ComboPath_with_params);
            });
        }

        
        // Блокировка всех кнопок по умолчанию
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            User_mode_bnt.IsChecked = true;
            foreach (var button in Backend.FindVisualChildren<Button>(this))
            {
                if (button.Name != "OpenfileBtn")
                {
                    button.IsEnabled = false;
                }
            }
        }

        private void Window_Block_All()
        {
            foreach (var button in Backend.FindVisualChildren<Button>(this))
            {
                if (button.Name != "OpenfileBtn")
                {
                    button.IsEnabled = false;
                }
            }
        }

        // Логика для кнопочек
        private void Banana_mode_bnt_Checked(object sender, RoutedEventArgs e)
        {
            alpha_threshold_TextBox.IsEnabled = true;
            start_offset_TextBox.IsEnabled = true;
            refractory_period_TextBox.IsEnabled = true;
        }

        private void User_mode_bnt_Checked(object sender, RoutedEventArgs e)
        {
            alpha_threshold_TextBox.IsEnabled = false;
            start_offset_TextBox.IsEnabled = false;
            refractory_period_TextBox.IsEnabled = false;

            alpha_threshold_TextBox.Text = alpha_threshold.ToString();
            start_offset_TextBox.Text = start_offset.ToString();
            refractory_period_TextBox.Text = refractory_period.ToString();
            window_size_TextBox.Text = window_size.ToString();
        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            // Блокируем все от шаловливых ручек пользователя
            Window_Block_All();

            // Добро пожаловать в питон
            await RunLongOperationAsync();

            // сохранение пути к выбранному файлу в переменной raw_filepath и "/" заменяем "\" на "/"
            FileNameTextBox.Text = System.IO.Path.GetFileName(raw_filepath); // извлекаем имя файла;

            // Разблокируем кнопочки
            SaveAP_Btn.IsEnabled = true;
            FindAPBtn.IsEnabled = true;
            FindAP_by_number_Btn.IsEnabled = true;
            UpdateBtn.IsEnabled = true;

            // Кнопка устарела
            //PlotAllBtn.IsEnabled = true;

            // Строим все графики, кроме одиночного ПД
            await PlotAllAsync();
        }
    }
}
