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
using System.Text.RegularExpressions;

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

            // Запуск на весь экран по умолчанию и запрет изменения размера окна

            _initialHeight = this.Height;
            _initialWidth = this.Width;

            this.SizeChanged += MainWindow_SizeChanged;
            this.StateChanged += MainWindow_StateChanged;

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
            One_AP_Plot.Plot.SetAxisLimitsY(-150, 110);

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

            One_AP_Plot.Plot.Benchmark(enable: true);
            All_AP_Plot.Plot.Benchmark(enable: true);
            RdV0_Plot.Plot.Benchmark(enable: true);
            RdV4_Plot.Plot.Benchmark(enable: true);
            dR_Plot.Plot.Benchmark(enable: true);
        }

        // УСТАРЕЛО
        //string pythonExePath = "C:/Python39/python.exe";

        string pythonExePath = "";
        string txtFilePath = "Python_FILE_PATH.txt";

        private double _initialHeight;
        private double _initialWidth;

        // Объявление переменных
        string raw_filepath = "c:\\";
        string separating_path = "";
        string ComboPath_with_params = "";
        string savedFilePath = "";
        string bebra = "";
        string save_error = "";
        string raw_time = "";

        double[][] intervals;
        double[] phase_0_speed_array;
        double[] phase_4_speed_array;
        double[] num_of_APs;
        double[] radius_array;
        double[][] x_y_array;
        double[][] x_y_for_offset_array;

        int time_ms = 0;
        int window_size = 3;

        string targetFile = "";
        double start_time = 0;
        double end_time = 0;

        double current_radius = 0;
        double phase_0_speed = 0;
        double phase_4_speed = 0;
        double x_local = 0;
        double y_local = 0;

        double x_offset = 0;
        double y_offset = 0;

        double alpha_threshold = 0.9;
        int start_offset = 0;
        int refractory_period = 280;
        double limit_radius = 250;

        bool check_ForFirstOpen = false;
        bool Params = false;
        bool user_or_banana = false;

        // выбрать файлик
        private async void OpenfileBtn_Click(object sender, RoutedEventArgs e)
        {
            // создание диалогового окна выбора файла
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // настройка параметров диалогового окна
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (!check_ForFirstOpen)
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
            bool Params = double.TryParse(alpha_threshold_TextBox.Text.Trim().Replace('.', ','), out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), out refractory_period) & double.TryParse(limit_radius_TextBox.Text.Trim().Replace('.', ','), out limit_radius);

            if (Params)
            {
                alpha_threshold = Math.Round(alpha_threshold, 3);
                limit_radius = Math.Round(limit_radius, 3);

                // отображение диалогового окна
                if (openFileDialog.ShowDialog() == true)
                {
                    raw_filepath = openFileDialog.FileName;
                    //raw_filepath = RenameRussianToLatin(raw_filepath);

                    // Код аналогичный UpdateBtn_Click ---

                    // Блокируем все от шаловливых ручек пользователя
                    Window_Block_All_Btns();

                    // Добро пожаловать в питон
                    await RunLongOperationAsync();

                    // сохранение пути к выбранному файлу в переменной raw_filepath и "/" заменяем "\" на "/"
                    FileNameTextBox.Text = System.IO.Path.GetFileName(raw_filepath); // извлекаем имя файла;

                    // Разблокируем ВСЕ кнопочки
                    Window_UnBlock_All_Btns();
                    // Блочим кнопку питона, она только для бананусов
                    Python_file_by_hand_btn.IsEnabled = false;

                    // Строим все графики, кроме одиночного ПД
                    await PlotAllAsync();

                    // Конец кода аналогичного UpdateBtn_Click ---

                    FindAP_by_number_Btn_Click(sender, e);

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
                string raw_filepath_with_params = raw_filepath + "\n" + alpha_threshold.ToString() + "\n" + start_offset.ToString() + "\n" + refractory_period.ToString() + "\n" + limit_radius.ToString();
                string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Save_APs_to_C#.py");
                var pythonData = Backend.RunPythonScript_AllPD(pythonExePath, scriptPath, raw_filepath_with_params); 
                
                separating_path = pythonData.Item1;
                intervals = pythonData.Item2;
                phase_0_speed_array = pythonData.Item3;
                phase_4_speed_array = pythonData.Item4;
                num_of_APs = pythonData.Item5;
                radius_array = pythonData.Item6;
                x_y_array = pythonData.Item7;
                x_y_for_offset_array = pythonData.Item8;

                check_ForFirstOpen = true;
            });
        }

        private async Task RunLongOperationAsync_ForSeparated_File()
        {
            await Task.Run(() =>
            {
                // Здесь выполняется самая длительная операция
                string raw_filepath_with_params = raw_filepath + "\n" + alpha_threshold.ToString() + "\n" + start_offset.ToString() + "\n" + refractory_period.ToString() + "\n" + limit_radius.ToString();
                string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PythonScripts", "Circle_to_C#.py");
                // Достаем данные для круга и скоростей
                Tuple<double, double, double, double, double, double, double> CircleData = Backend.RunPythonScriptCircle(pythonExePath, scriptPath, raw_filepath_with_params);
                current_radius = CircleData.Item1;
                x_local = CircleData.Item2;
                y_local = CircleData.Item3;
                phase_0_speed = CircleData.Item4;
                phase_4_speed = CircleData.Item5;
                x_offset = CircleData.Item6;
                y_offset = CircleData.Item7;
            });
        }


        // Кнопка выполняет функуцию обновления графика
        private void PlotAllBtn_Click(object sender, RoutedEventArgs e)
        {
            // вся логика вынесена в отдельную функцию
            UpdateStatisticAsync();
        }

        // Строим все графики, кроме одиночного ПД
        private async Task PlotAllAsync()
        {
            // проверка на window size
            if (!int.TryParse(window_size_TextBox.Text.Trim(), out int window_size) | !(window_size <= num_of_APs.Length - 1))
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

                All_AP_Plot.Plot.SetAxisLimitsY(-100, 60);

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
                if (user_or_banana)
                {
                    RdV0_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV0, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineWidth: 0, markerSize: 10);
                    RdV0_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV0, null, confidenceInterval_dV0);

                    RdV4_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV4, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineWidth: 0, markerSize: 10);
                    RdV4_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV4, null, confidenceInterval_dV4);

                    dR_Plot.Plot.AddScatter(num_of_APs_window_size, means_dR, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Blue), lineWidth: 0, markerSize: 10);
                    dR_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dR, null, confidenceInterval_dR);

                }
                else
                {
                    RdV0_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV0, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineStyle: LineStyle.Dot);
                    RdV0_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV0, null, confidenceInterval_dV0);

                    RdV4_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV4, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineStyle: LineStyle.Dot);
                    RdV4_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV4, null, confidenceInterval_dV4);

                    dR_Plot.Plot.AddScatter(num_of_APs_window_size, means_dR, lineStyle: LineStyle.Dot);
                    dR_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dR, null, confidenceInterval_dR);
                }


                //RdV0_Plot.Plot.Benchmark(enable: true);
                //RdV4_Plot.Plot.Benchmark(enable: true);
                //dR_Plot.Plot.Benchmark(enable: true);

                RdV0_Plot.Refresh();
                RdV4_Plot.Refresh();
                dR_Plot.Refresh();
            }
        }

        private void UpdateStatisticAsync()
        {
            // проверка на window size
            if (!int.TryParse(window_size_TextBox.Text.Trim(), out int window_size) | !(window_size <= num_of_APs.Length - 1))
            {
                MessageBox.Show("Decrease the value of \"window_size\" or set it correctly");
            }
            else
            {
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
                if (user_or_banana)
                {
                    RdV0_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV0, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineWidth: 0, markerSize: 10);
                    RdV0_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV0, null, confidenceInterval_dV0);

                    RdV4_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV4, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineWidth: 0, markerSize: 10);
                    RdV4_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV4, null, confidenceInterval_dV4);

                    dR_Plot.Plot.AddScatter(num_of_APs_window_size, means_dR, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Blue), lineWidth: 0, markerSize: 10);
                    dR_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dR, null, confidenceInterval_dR);

                }
                else
                {
                    RdV0_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV0, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineStyle: LineStyle.Dot);
                    RdV0_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV0, null, confidenceInterval_dV0);

                    RdV4_Plot.Plot.AddScatter(num_of_APs_window_size, means_dV4, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), lineStyle: LineStyle.Dot);
                    RdV4_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dV4, null, confidenceInterval_dV4);

                    dR_Plot.Plot.AddScatter(num_of_APs_window_size, means_dR, lineStyle: LineStyle.Dot);
                    dR_Plot.Plot.AddErrorBars(num_of_APs_window_size, means_dR, null, confidenceInterval_dR);
                }

                //RdV0_Plot.Plot.Benchmark(enable: true);
                //RdV4_Plot.Plot.Benchmark(enable: true);
                //dR_Plot.Plot.Benchmark(enable: true);

                RdV0_Plot.Refresh();
                RdV4_Plot.Refresh();
                dR_Plot.Refresh();
            }
        }

        private async Task ProcessFileAsync(string filePath)
        {
            var data = await Task.Run(() => Backend.ReadFileData_ForAllPlot(filePath));

            // Построение графика
            All_AP_Plot.Plot.AddScatterLines(data.Times.ToArray(), data.Potentials.ToArray(), lineWidth: 5, color: System.Drawing.Color.Black);
        }

        // найти по заданному времени участок и построить графики
        private void FindAPBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Чтобы 2 потока не боролись за одни и те же данные
                UpdateStatisticBtn.IsEnabled = false;

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
                UpdateStatisticBtn.IsEnabled = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Возвращаем возможность строить остальное
                UpdateStatisticBtn.IsEnabled = true;
            }
        }

        private void FindAP_by_number_Btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Чтобы 2 потока не боролись за одни и те же данные
                UpdateStatisticBtn.IsEnabled = false;

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
                UpdateStatisticBtn.IsEnabled = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Возвращаем возможность строить остальное
                UpdateStatisticBtn.IsEnabled = true;
            }
        }
        

        // общий метод построения графиков в маленьком окошке и смещения красных прямых
        void Plot_And_Refresh()
        {
            // Проверим, не сломал ли пользователь параметры
            Params = double.TryParse(alpha_threshold_TextBox.Text.Trim().Replace('.', ','), out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), out refractory_period) & double.TryParse(limit_radius_TextBox.Text.Trim().Replace('.', ','), out limit_radius);

            string targetFile_with_params = targetFile + "\n" + alpha_threshold.ToString() + "\n" + start_offset.ToString() + "\n" + refractory_period.ToString() +"\n" + limit_radius.ToString();
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

                    int currentFileNum = Convert.ToInt32(Path.GetFileNameWithoutExtension(targetFile));

                    // Строим заготовку
                    One_AP_Plot.Plot.AddScatterLines(time, voltage, lineWidth: 5, label: $"R = {radius_array[currentFileNum-1]}\r\nN = {currentFileNum}");

                    One_AP_Plot.Plot.AddMarker(x_y_for_offset_array[currentFileNum - 1][0], x_y_for_offset_array[currentFileNum - 1][1], MarkerShape.filledCircle, 15, System.Drawing.Color.GreenYellow);

                    // calculate the first derivative
                    double[] deriv = new double[voltage.Length];
                    deriv = Backend.CalculateDerivative(voltage, time);

                    // plot the first derivative in red on the secondary Y axis
                    var dVdt = One_AP_Plot.Plot.AddScatterLines(time, deriv, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), label: $"(dV/dt)0 =  {phase_0_speed_array[currentFileNum-1]} V/s\r\n(dV/dt)4 = {phase_4_speed_array[currentFileNum - 1]} mV/s");
                    dVdt.YAxisIndex = 1;
                    dVdt.LineWidth = 3;
                    var legend = One_AP_Plot.Plot.Legend(enable: true);
                    legend.Orientation = ScottPlot.Orientation.Horizontal;


                    // Добавляем красные хуйни
                    All_AP_Plot.Plot.AddVerticalLine(start_time, color: System.Drawing.Color.Red, style: LineStyle.Solid);
                    All_AP_Plot.Plot.AddVerticalLine(end_time, color: System.Drawing.Color.Red, style: LineStyle.Solid);

                    // Добавляем на график круг
                    One_AP_Plot.Plot.AddCircle(x_y_array[currentFileNum-1][0], x_y_array[currentFileNum-1][1], radius_array[currentFileNum - 1], color: System.Drawing.Color.Red);

                    // добавление горизорнтальной полоски Овершут
                    One_AP_Plot.Plot.AddHorizontalLine(0, color: System.Drawing.Color.Gray, style: LineStyle.Dash);
                    One_AP_Plot.Plot.SetAxisLimitsY(-150, 110);

                    // Переход на большом графике к интересному месту
                    All_AP_Plot.Plot.SetAxisLimitsX(start_time - 1000, end_time + 1000);
                    All_AP_Plot.Plot.SetAxisLimitsY(-100, 60);

                    // используем наш custom formatter для формата времени под mm:ss:ff на маленьком графике
                    One_AP_Plot.Plot.XAxis.TickLabelFormat(Backend.customTickFormatter);


                    //One_AP_Plot.Plot.Benchmark(enable: true);
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

        void Plot_OneAP()
        {
            // Очистка графика перед построением
            One_AP_Plot.Plot.Clear();
            double[] time, voltage;
            if (Backend.ParseFileData(raw_filepath, out time, out voltage))
            {
                // Строим заготовку
                One_AP_Plot.Plot.AddScatterLines(time, voltage, lineWidth: 5, label: $"R = {current_radius}");

                One_AP_Plot.Plot.AddMarker(x_offset, y_offset, MarkerShape.filledCircle, 15, System.Drawing.Color.GreenYellow);

                // calculate the first derivative
                double[] deriv = new double[voltage.Length];
                deriv = Backend.CalculateDerivative(voltage, time);

                // plot the first derivative in red on the secondary Y axis
                var dVdt = One_AP_Plot.Plot.AddScatterLines(time, deriv, color: System.Drawing.Color.FromArgb(120, System.Drawing.Color.Red), label: $"(dV/dt)0 =  {phase_0_speed} V/s\r\n(dV/dt)4 = {phase_4_speed} mV/s");
                dVdt.YAxisIndex = 1;
                dVdt.LineWidth = 3;
                var legend = One_AP_Plot.Plot.Legend(enable: true);
                legend.Orientation = ScottPlot.Orientation.Horizontal;

                // Добавляем на график круг
                One_AP_Plot.Plot.AddCircle(x_local, y_local, current_radius, color: System.Drawing.Color.Red);

                // добавление горизорнтальной полоски Овершут
                One_AP_Plot.Plot.AddHorizontalLine(0, color: System.Drawing.Color.Gray, style: LineStyle.Dash);

                One_AP_Plot.Plot.SetAxisLimitsY(-150, 110);

                // используем наш custom formatter для формата времени под mm:ss:ff на маленьком графике
                One_AP_Plot.Plot.XAxis.TickLabelFormat(Backend.customTickFormatter);
                
                // Обновление графика
                //One_AP_Plot.Plot.Benchmark(enable: true);
                One_AP_Plot.Refresh();
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
            // !! Если пользователь хочет сохранить в табличку файл, который он еще не открыл
            if (Path.GetFileName(raw_filepath) == "")
            {
                // создание диалогового окна выбора файла
                OpenFileDialog openFileDialog = new OpenFileDialog();

                // настройка параметров диалогового окна
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

                if (!check_ForFirstOpen)
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

                // отображение диалогового окна
                if (openFileDialog.ShowDialog() == true)
                {
                    raw_filepath = openFileDialog.FileName;

                    FileNameTextBox.Text = System.IO.Path.GetFileName(raw_filepath); // извлекаем имя файла;

                    // Т.к. уже выбрали файл, то при вызове этой же функции, этот if будет пропущен
                    SaveAP_Btn_Click(sender, e);
                }

            }
            // Обычное сохранение после просмотра информации о файле и графиков (!как было раньше!)
            else
            {
                // Тут что-то не так
                bool ihatenigg = false;
                OpenfileBtn.IsEnabled = ihatenigg;
                UpdateBtn.IsEnabled = ihatenigg;

                savedFilePath = Backend.SaveFileAndGetPath(raw_filepath);
                string ComboPath = savedFilePath + "\n" + raw_filepath;

                // Работу с TextBox закинем в основной поток от греха подальше
                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Проверим, не сломал ли пользователь параметры
                    Params = double.TryParse(alpha_threshold_TextBox.Text.Trim(), out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), out refractory_period) & double.TryParse(limit_radius_TextBox.Text.Trim(), out limit_radius);

                }));

                ComboPath_with_params = ComboPath + "\n" + alpha_threshold.ToString() + "\n" + start_offset.ToString() + "\n" + refractory_period.ToString() +"\n" + limit_radius.ToString();
                
                // Теперь как надо
                ihatenigg = true;

                if (Params)
                {
                    alpha_threshold = Math.Round(alpha_threshold, 3);

                    if (!string.IsNullOrEmpty(savedFilePath))
                    {
                        // Лезем в питон сохранять все в табличку
                        await RunLongSaveAsync();
                        if (bebra == "true") { MessageBox.Show($"File saved successfully to\r\n{savedFilePath}"); }
                        else if (bebra == "false") { MessageBox.Show($"An error occurred while saving. Maybe you have a *.xlsx table open that you are trying to overwrite.\r\n{save_error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
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
                OpenfileBtn.IsEnabled = ihatenigg;
                UpdateBtn.IsEnabled = ihatenigg;
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

                string[] lines = bebra.Split('\n');
                bebra = lines[0].TrimEnd('\r');
                save_error = lines[1].TrimEnd('\r');
            });
        }

        
        // Блокировка всех кнопок по умолчанию
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            User_mode_bnt.IsChecked = true;
            foreach (var button in Backend.FindVisualChildren<Button>(this))
            {
                button.IsEnabled = false;
            }
            foreach (var textbox in Backend.FindVisualChildren<TextBox>(this))
            {
                textbox.IsEnabled = false;
            }
            foreach (var radiobtn in Backend.FindVisualChildren<RadioButton>(this))
            {
                radiobtn.IsEnabled = false;
            }

            SaveAP_Btn.IsEnabled = true;
            Experiment_duration_label.IsEnabled = true;
            SingleFileBtn.IsEnabled = true;

            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, txtFilePath)))
            {
                pythonExePath = File.ReadAllText(txtFilePath);
                if (File.Exists(pythonExePath) && Path.GetFileName(pythonExePath) == "python.exe")
                {
                    // Если все ок, то можем продолжить работу, иначе пользователь не имеет доступа к программе
                    OpenfileBtn.IsEnabled = true;
                    window_size_TextBox.IsEnabled = true;
                    TimeTextBox.IsEnabled = true;
                    NumberAP_TextBox.IsEnabled = true;
                    
                    // В новой версии без Banana mode нельзя поменять выставленный вначале питон.ехе
                    //Python_file_by_hand_btn.IsEnabled = true;
                    foreach (var radiobtn in Backend.FindVisualChildren<RadioButton>(this))
                    {
                        radiobtn.IsEnabled = true;
                    }
                }
                else
                {
                    // Удалим файл, если он битый
                    File.Delete(txtFilePath);

                    string prompt = "С файлом python.exe что-то не так. Хотите указать корректный файл вручную?";

                    Tuple<bool, string> python_file_data = Backend.PromptForPythonExe(prompt, txtFilePath, pythonExePath);
                    bool bool_python_file = python_file_data.Item1;
                    pythonExePath = python_file_data.Item2;

                    if (bool_python_file)
                    {
                        // Если все ок, то можем продолжить работу, иначе пользователь не имеет доступа к программе
                        OpenfileBtn.IsEnabled = true;
                        window_size_TextBox.IsEnabled = true;
                        TimeTextBox.IsEnabled = true;
                        NumberAP_TextBox.IsEnabled = true;
                        Python_file_by_hand_btn.IsEnabled = true;
                        foreach (var radiobtn in Backend.FindVisualChildren<RadioButton>(this))
                        {
                            radiobtn.IsEnabled = true;
                        }
                    }
                }
            }
            else
            {
                string prompt = "Файл python.exe не найден. Хотите указать его вручную?";

                Tuple<bool, string> python_file_data = Backend.PromptForPythonExe(prompt, txtFilePath, pythonExePath);
                bool bool_python_file = python_file_data.Item1;
                pythonExePath = python_file_data.Item2;

                if (bool_python_file)
                {
                    // Если все ок, то можем продолжить работу, иначе пользователь не имеет доступа к программе
                    OpenfileBtn.IsEnabled = true;
                    window_size_TextBox.IsEnabled = true;
                    foreach (var radiobtn in Backend.FindVisualChildren<RadioButton>(this))
                    {
                        radiobtn.IsEnabled = true;
                    }
                }
            }
        }
        

        private void Window_Block_All_Btns()
        {
            foreach (var button in Backend.FindVisualChildren<Button>(this))
            {
                button.IsEnabled = false;
            }
        }
        private void Window_UnBlock_All_Btns()
        {
            foreach (var button in Backend.FindVisualChildren<Button>(this))
            {
                button.IsEnabled = true;
            }
        }

        // Логика для Radio кнопочек
        private void Banana_mode_bnt_Checked(object sender, RoutedEventArgs e)
        {
            alpha_threshold_TextBox.IsEnabled = true;
            start_offset_TextBox.IsEnabled = true;
            refractory_period_TextBox.IsEnabled = true;
            limit_radius_TextBox.IsEnabled = true;
            UpdateBtn.IsEnabled = true;

            user_or_banana = true;
        }

        private void User_mode_bnt_Checked(object sender, RoutedEventArgs e)
        {
            alpha_threshold_TextBox.IsEnabled = false;
            start_offset_TextBox.IsEnabled = false;
            refractory_period_TextBox.IsEnabled = false;
            limit_radius_TextBox.IsEnabled = false;
            UpdateBtn.IsEnabled = false;
            Python_file_by_hand_btn.IsEnabled = false;

            user_or_banana = false;

            alpha_threshold_TextBox.Text = alpha_threshold.ToString();
            start_offset_TextBox.Text = start_offset.ToString();
            refractory_period_TextBox.Text = refractory_period.ToString();
            limit_radius_TextBox.Text = limit_radius.ToString();
            window_size_TextBox.Text = window_size.ToString();
        }

        // Если пользователь изменил параметры и не хочет заново выбирать файл
        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            // Проверим, не сломал ли пользователь параметры
            bool Params = double.TryParse(alpha_threshold_TextBox.Text.Trim().Replace('.', ','), out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), out refractory_period) & double.TryParse(limit_radius_TextBox.Text.Trim().Replace('.', ','), out limit_radius);

            if (Params)
            {
                alpha_threshold = Math.Round(alpha_threshold, 3);
                limit_radius = Math.Round(limit_radius, 3);



                // Блокируем все от шаловливых ручек пользователя
                Window_Block_All_Btns();

                // Добро пожаловать в питон
                await RunLongOperationAsync();

                // сохранение пути к выбранному файлу в переменной raw_filepath и "/" заменяем "\" на "/"
                FileNameTextBox.Text = System.IO.Path.GetFileName(raw_filepath); // извлекаем имя файла;

                // Разблокируем кнопочки
                Window_UnBlock_All_Btns();
                Python_file_by_hand_btn.IsEnabled = false;


                // Кнопка устарела
                //PlotAllBtn.IsEnabled = true;

                // Строим все графики, кроме одиночного ПД
                await PlotAllAsync();
            }
        }


        private void ChangeAP_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is WpfPlot) // или любой другой элемент, который вы хотите исключить
            {
                e.Handled = true; // поглощает событие
                return;
            }
            if (e.Key == Key.Right)
            {
                // Обработка нажатия стрелки вправо
                Next_AP_Btn_Click(sender, e);
            }
            if (e.Key == Key.Left)
            {
                Previos_AP_Btn_Click(sender, e);
            }
        }

        // Если пользователь ебобо и решил полезь сам менять питон по ходу работы
        private void Python_file_by_hand_btn_Click(object sender, RoutedEventArgs e)
        {
            bool local_check = false;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Executable files (*.exe)|*.exe";

            if (openFileDialog.ShowDialog() == true)
            {
                if (Path.GetFileName(openFileDialog.FileName) != "python.exe")
                {
                    MessageBoxResult anotherResult = MessageBox.Show("Вы уверены, что хотите выбрать этот файл?", "Предупреждение", MessageBoxButton.YesNo);
                    if (anotherResult != MessageBoxResult.Yes)
                    {
                        local_check = true;
                    }
                }
                if (!local_check)
                {
                    pythonExePath = openFileDialog.FileName;
                    File.WriteAllText(txtFilePath, pythonExePath);
                }
            }
        }

        // Методы для запрета изменения размера окна
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState != WindowState.Maximized)
            {
                this.Height = _initialHeight;
                this.Width = _initialWidth;
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                // Позволяем изменить размер окна
            }
            else
            {
                this.Height = _initialHeight;
                this.Width = _initialWidth;
            }
        }

        private void ClearAllBtn_Click(object sender, RoutedEventArgs e)
        {
            One_AP_Plot.Plot.Clear();
            All_AP_Plot.Plot.Clear();
            RdV0_Plot.Plot.Clear();
            RdV4_Plot.Plot.Clear();
            dR_Plot.Plot.Clear();

            One_AP_Plot.Refresh();
            All_AP_Plot.Refresh(); ;
            RdV0_Plot.Refresh();
            RdV4_Plot.Refresh();
            dR_Plot.Refresh();

            raw_filepath = "c:\\";
            separating_path = "";
            ComboPath_with_params = "";
            savedFilePath = "";
            bebra = "";
            save_error = "";
            raw_time = "";

            time_ms = 0;
            window_size = 3;
            targetFile = "";
            start_time = 0;
            end_time = 0;

            current_radius = 0;
            phase_0_speed = 0;
            phase_4_speed = 0;

            FileNameTextBox.Text = "Here will be name of opened file";
            TimeTextBox.Text = "00:00:00";
            NumberAP_TextBox.Text = "1";

            Window_Loaded(sender, e);
        }

        // Для открытия порезанного файла
        private async void SingleFileBtn_Click(object sender, RoutedEventArgs e)
        {
            // создание диалогового окна выбора файла
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // настройка параметров диалогового окна
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (!check_ForFirstOpen)
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
            bool Params = double.TryParse(alpha_threshold_TextBox.Text.Trim().Replace('.', ','), out alpha_threshold) & int.TryParse(start_offset_TextBox.Text.Trim(), out start_offset) & int.TryParse(refractory_period_TextBox.Text.Trim(), out refractory_period) & double.TryParse(limit_radius_TextBox.Text.Trim().Replace('.', ','), out limit_radius);

            if (Params)
            {
                alpha_threshold = Math.Round(alpha_threshold, 3);
                limit_radius = Math.Round(limit_radius, 3);

                // отображение диалогового окна
                if (openFileDialog.ShowDialog() == true)
                {
                    raw_filepath = openFileDialog.FileName;
                    //raw_filepath = RenameRussianToLatin(raw_filepath);

                    // Добро пожаловать в питон
                    await RunLongOperationAsync_ForSeparated_File();

                    // сохранение пути к выбранному файлу в переменной raw_filepath и "/" заменяем "\" на "/"
                    FileNameTextBox.Text = System.IO.Path.GetFileName(raw_filepath) + " IS SEPARATED FILE"; // извлекаем имя файла;

                    // Строим все графики, кроме одиночного ПД
                    Plot_OneAP();
                }
            }
            else
            {
                MessageBox.Show("Check next inputs:\nalpha-threshold\nstart-offset\nrefractory-period", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
