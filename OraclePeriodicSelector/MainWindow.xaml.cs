using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; //Библиотека для мультипоточности
using System.Threading; //Библиотека для мультипоточности
using System.Windows.Threading;
using System.Windows;
using System.Timers;
//using static System.Windows.Forms.Timer;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO; //Библиотека для работы с системой I/O
using System.Windows.Interop; //Библиотека для моргания в панели задач
using System.Runtime.InteropServices; //Библиотека для моргания в панели задач
using Oracle.ManagedDataAccess.Client;
using System.Reflection;

namespace OraclePeriodicSelector
{
    public class SupportFunc
    {
        public string ReturnTimeString()
        { return string.Format("{0}{1}{2}{1}{0}", Environment.NewLine, "=======", DateTime.Now); }

        public string GetFileToExecute(string fileName)
        {
            FileInfo file = new FileInfo(@fileName); //Получаем весь файл, где должен лежать селект без ";" 
            try
            {
                //TODO: Надо сделать следующее: Если последний символ это ; то мы его игнорируе
                return file.OpenText().ReadToEnd();
            }
            catch (FileNotFoundException)
            {
                return "-1";
            }
        }

        public void LogIntoFile(string log_info)
        {
            File.AppendAllText(@"log.txt", string.Format("{0}{1}", Environment.NewLine, log_info));
        }
    }

    public class OracleSupport {

        SupportFunc SF = new SupportFunc();
        //Объявляем глобальный таймер
        DispatcherTimer timer = new DispatcherTimer();

        //Делегат для обновления результат
        public delegate void ResultTextUpdaterDelegat(string result);
        //Евент для обновления результат
        public event ResultTextUpdaterDelegat SetResultEvent;

        //Делегат для остановки выполнения по таймеру
        public delegate void StopTimerExecuteDelegat();
        //Евент для остановки выполнения по таймеру
        public event StopTimerExecuteDelegat TimerExecuteStopEvent;

        //Делегат для остановки выполнения по таймеру
        public delegate void FlashIconDelegate();
        //Евент для остановки выполнения по таймеру
        public event FlashIconDelegate FlashIconEvent;

        public void StartExecute(object sender, EventArgs e)
        {
            string result = "";

            //Получаем необходимые настройки для подключения
            string login         = Properties.Settings.Default.loginGL;
            string pass          = Properties.Settings.Default.passwordGL;
            string host          = Properties.Settings.Default.hostGL;
            string port          = Properties.Settings.Default.portGL;
            string serviceName   = Properties.Settings.Default.serviceNameGL;
            string fileToExecute = Properties.Settings.Default.fileToExecute;

            //Получаем файл для выполнения
            string sqlToExecute = SF.GetFileToExecute(fileToExecute);

            if (sqlToExecute == "-1")
            {
                SetResultEvent(string.Format("{0} Файл не найден. Создайте файл {1} в папке приложения:{2}{3} ", SF.ReturnTimeString(), fileToExecute, Environment.NewLine, Directory.GetCurrentDirectory()));
                TimerExecuteStopEvent();
                return;
            }

            string connectionString = string.Format("User Id = {3}; Password = {4};Data Source = (DESCRIPTION = (ADDRESS = (PROTOCOL = TCP)(HOST = {0})(PORT = {1}))(CONNECT_DATA = (SERVICE_NAME = {2})))",
                           host, port, serviceName, login, pass);

            OracleConnection conn = new OracleConnection(connectionString);

            try
            {
                conn.Open();
            }
            catch (OracleException)
            {
                SetResultEvent(string.Format("{0}Невозможно подключиться к БД с указанными параметрами. Проверьте данные для подключения.", SF.ReturnTimeString()));
                TimerExecuteStopEvent();
                return;
                //Необходимо сделать событие по которому будем прерывать выполнение, обновлять кнопки и прочее.
            }
                
            OracleCommand command = new OracleCommand(sqlToExecute, conn);  //Выполняем команду в оралке. Передаем собственно команду и объект с открытым коннектом

            using (OracleDataReader reader = command.ExecuteReader()) //По факту мы просто юзаем метод получения всех строк из селекта 
            { 
                if (reader.HasRows) //Если ридер имеет строки, прозаично
                {
                   while (reader.Read()) //Проходимся по массиву данных который вернули reader
                    {
                       result = string.Format("{0}{1}{2}", result, Environment.NewLine, reader.GetString(0));
                    }
                    FlashIconEvent(); //Моргаем в панели задач
                }
                    else { result = "Ни одной строки не получено."; } //Если в селекте ничего нет, то пишем такой результат
            }
            command.Connection.Close(); //Закрываем подключение

            SetResultEvent(string.Format("{0}{1}", SF.ReturnTimeString(),result));
        }

        public void ExecuteStop()
        { timer.Stop(); }

        public void OnTimerStartExecute()
        {
            timer.Tick += new EventHandler(StartExecute);
            timer.Start(); //Да, сначала старт, потом задаем интервал
            timer.Interval = new TimeSpan(0, Properties.Settings.Default.checkInterval, 0); 
        }
    }

    public partial class MainWindow : Window
    {
        OracleSupport OS = new OracleSupport(); //создаем копию объекта с бизнес-логикой
        SupportFunc   SF = new SupportFunc();

        public MainWindow()
        {
            InitializeComponent();

            //Восстанавливаем настройки
            loginInput.Text          = Properties.Settings.Default.loginGL;
            passwordInput.Password   = Properties.Settings.Default.passwordGL;
            hostInput.Text           = Properties.Settings.Default.hostGL;
            portInput.Text           = Properties.Settings.Default.portGL;
            serviceNameInput.Text    = Properties.Settings.Default.serviceNameGL;
            rememberInputs.IsChecked = Properties.Settings.Default.savedSettings;
            intervalCombo.Text       = Convert.ToString(Properties.Settings.Default.checkInterval);
            timerCheck.IsChecked     = Properties.Settings.Default.regularCheck;
            Title                    = Properties.Settings.Default.titleApplication;

            OS.SetResultEvent        += SF.LogIntoFile;   //Подписываем логирование результата по событию SetResultEvent
            OS.SetResultEvent        += ResultUpdate;     //Подписываем обновление результата по событию SetResultEvent
            OS.TimerExecuteStopEvent += OS.ExecuteStop;   //Подписываем завершение выполнения по таймеру по событию TimerExecuteStopEvent
            OS.TimerExecuteStopEvent += ButtonStartState; //Подписываем установку кнопок в начальное состояние по событию TimerExecuteStopEvent
            OS.FlashIconEvent        += FlashToolPanel;
        }

        [DllImport("user32")]
        public static extern int FlashWindow(IntPtr hwnd, bool bInvert);

        public void FlashToolPanel()
        {
            Dispatcher.Invoke(() => {
                //Моргаем значком в панели задач
                WindowInteropHelper wih = new WindowInteropHelper(mainWindow);
                FlashWindow(wih.Handle, true);
            });
        }

        //Метод для проверки поля на только числовые значения
        private void OnlyDigitInputCheck(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, e.Text.Length - 1))
                e.Handled = true;
        }

        //Обновляем результат по евенту
        protected void ResultUpdate(string result)
        {
            Dispatcher.Invoke(() => { resultText.Text = result; });
        }

        public Tuple<string, int> CheckAppParameters()
        {
            string errorDescription = "Заполните поля:";
            int status = 1;

            if (loginInput.Text == "") 
            { errorDescription = string.Format("{0} Логин;", errorDescription); status = -1; }

            if (passwordInput.Password == "")
            { errorDescription = string.Format("{0} Пароль;", errorDescription); status = -1; }

            if (hostInput.Text == "")
            { errorDescription = string.Format("{0} Адрес;", errorDescription); status = -1; }

            if (portInput.Text == "")
            { errorDescription = string.Format("{0} Порт;", errorDescription); status = -1; }

            if (serviceNameInput.Text == "")
            { errorDescription = string.Format("{0} Имя сервиса;", errorDescription); status = -1; }
                
            return Tuple.Create(errorDescription, status);
        }

        protected void SaveAppParameters()
        {
            //Все введенные параметры запишем как настройки для всего приложения.
            //Таким образом далее мы не будет зависеть от интерфейса
            Properties.Settings.Default.loginGL = loginInput.Text;
            Properties.Settings.Default.passwordGL = passwordInput.Password;
            Properties.Settings.Default.hostGL = hostInput.Text;
            Properties.Settings.Default.portGL = portInput.Text;
            Properties.Settings.Default.serviceNameGL = serviceNameInput.Text;
            Properties.Settings.Default.savedSettings = Convert.ToBoolean(rememberInputs.IsChecked);
            Properties.Settings.Default.checkInterval = Convert.ToInt32(intervalCombo.Text);
            Properties.Settings.Default.regularCheck = Convert.ToBoolean(timerCheck.IsChecked);
            Properties.Settings.Default.titleApplication = string.Format("OraclePeriodicSelector({0})", Properties.Settings.Default.hostGL);

            Title = Properties.Settings.Default.titleApplication;

            //Если есть галочка - сохраняем настройки
            if (Properties.Settings.Default.savedSettings == true)
            { Properties.Settings.Default.Save(); }
        }

        protected void ConnectButtonClick(object sender, RoutedEventArgs e)
        {
            if (CheckAppParameters().Item2 == -1)
            {
                ResultUpdate(CheckAppParameters().Item1);
                return;
            }

            SaveAppParameters();

            if (Properties.Settings.Default.regularCheck == true)
            {
                connectButton.IsEnabled = false;
                buttonStop.IsEnabled = true;
                OS.OnTimerStartExecute();      //Запускаем выполнение селекта по таймеру
            }
            else
            {
                connectButton.IsEnabled = false;
                OS.StartExecute(sender, e);    //Запускаем одиночное выполнение селекта
            }
        }

        protected void ButtonStartState()
        {
            connectButton.IsEnabled = true;
            buttonStop.IsEnabled = false;
        }

        protected void ButtonStopClick(object sender, RoutedEventArgs e)
        {
            OS.ExecuteStop();
            ButtonStartState();
        }
    }
}
