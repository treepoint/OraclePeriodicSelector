using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; //Библиотека для мультипоточности
using System.Threading; //Библиотека для мультипоточности
using System.Windows.Threading;
using System.Windows;
using System.Timers;
using static System.Windows.Forms.Timer;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Data.OracleClient; //Библиотека для подключения к Oracle
using System.IO; //Библиотека для работы с системой I/O
using System.Windows.Interop; //Библиотека для моргания в панели задач
using System.Runtime.InteropServices; //Библиотека для моргания в панели задач

namespace OraclePeriodicSelector
{
    public class SupportFunc
    {
       public string executeSelect()
        {
            return "Делай, делай селект";
        }

        //Объявляем глобальный таймер
        DispatcherTimer timer = new DispatcherTimer();

        //Делегат для обновления результат
        public delegate void ResultTextUpdater(string result);
        //Евент для обновления результат
        public event ResultTextUpdater setResult;

        public void TimerExecute(object sender, EventArgs e)
        {
            setResult("result" + DateTime.Now);
        }

        public void TimerStop()
        {
            timer.Stop(); //Да, сначала старт, потом задаем интервал
        }

        public void startExecute(Int32 checkInterval)
        {
            timer.Tick += new EventHandler(TimerExecute);
            timer.Start(); //Да, сначала старт, потом задаем интервал
            timer.Interval = new TimeSpan(0, 0, 1);
        }
    }

    public partial class MainWindow : Window
    {

        SupportFunc SF = new SupportFunc(); //создаем копию объекта с бизнес-логикой

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
        }

        //Обновляем результат
        public void updateResultText(string result)
        {
            Dispatcher.Invoke(() => {
                resultText.Text = result;
            });
        }

        private void ConnectButtonClick(object sender, RoutedEventArgs e)
        {
            //Все введенные параметры запишем как настройки для всего приложения.
            //Таким образом далее мы не будет зависеть от интерфейса
            Properties.Settings.Default.loginGL       = loginInput.Text;
            Properties.Settings.Default.passwordGL    = passwordInput.Password;
            Properties.Settings.Default.hostGL        = hostInput.Text;
            Properties.Settings.Default.portGL        = portInput.Text;
            Properties.Settings.Default.serviceNameGL = serviceNameInput.Text;
            Properties.Settings.Default.savedSettings = Convert.ToBoolean(rememberInputs.IsChecked);
            Properties.Settings.Default.checkInterval = Convert.ToInt32(intervalCombo.Text);
            Properties.Settings.Default.regularCheck  = Convert.ToBoolean(timerCheck.IsChecked);

            if (Properties.Settings.Default.savedSettings == true)  //Если есть галочка - сохраняем настройки
                {
                    Properties.Settings.Default.Save();
                }

            connectButton.IsEnabled = false;  //вырубаем кнопку Пуск
            buttonStop.IsEnabled = true;      //врубаем кнопку Стоп

            SF.setResult += updateResultText;  //Подписываем обновление результата по событию setResult
            SF.startExecute(Properties.Settings.Default.checkInterval);  //Запускаем выполнение селекта

        }

        private void ButtonStopClick(object sender, RoutedEventArgs e)
        {
            SF.TimerStop();
            connectButton.IsEnabled = true;  //вырубаем кнопку Пуск
            buttonStop.IsEnabled = false;    //вырубаем кнопку Пуск
        }
    }
}
