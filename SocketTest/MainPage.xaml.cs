using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace SocketTest
{
    public class Notify_Client : notify_info
    {
        public object build(byte[] _buff, out uint _use)
        {
            // 把_buff 转换为msg
            _use = 0;
            if (_buff.Length < 4)
            {
                return null;
            }

            var len = Convert.ToInt32(_buff);
            if (len + 4 <= _buff.Length)
            {
                _use = (uint)len + 4;
            }
            return System.Text.Encoding.UTF8.GetString(_buff.Skip(4).Take(len).ToArray());
        }
        public void on_close(socket_base _sock)
        {
            // 关闭
        }
        public void on_recv(socket_base _sock, object _msg)
        {
            // 收到消息
            Debug.WriteLine("服务器发来消息：" + _msg.ToString());
        }
        public byte[] serialize(object _msg)
        {
            // 把_msg 转换为byte[]
            var msg_s = _msg as string;
            if (null == msg_s)
            {
                return null;
            }

            byte[] r_byte = System.Text.Encoding.UTF8.GetBytes(msg_s);
            if (null == r_byte)
            {
                return null;
            }

            byte[] buff = new byte[4 + r_byte.Length];
            System.Buffer.BlockCopy(BitConverter.GetBytes(r_byte.Length), 0, buff, 0, 4);
            System.Buffer.BlockCopy(r_byte, 0, buff, 4, r_byte.Length);
            return buff;
        }
    }
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        socket_mgr sock_mgr_ = new socket_mgr();
        public MainPage()
        {
            this.InitializeComponent();
        }

        public object lock_ = new object();
        public List<string> send_msg_list_ = new List<string>();

        public void set_msg(string _msg)
        {
            lock (lock_)
            {
                send_msg_list_.Add(_msg);
            }
        }

        public string get_msg()
        {
            lock (lock_)
            {
                if (send_msg_list_.Count > 0)
                {
                    string ret = send_msg_list_[0];
                    send_msg_list_.RemoveAt(0);
                    return ret;
                }
                return null;
            }
        }

        private void connect_Click(object sender, RoutedEventArgs e)
        {
            sock_mgr_.begin("114.55.137.77", "6000");
            //sock_mgr_.begin("192.168.1.104", "5011");
            //sock_mgr_.begin("localhost", "1983");
            return;
        }

        private void write_Click(object sender, RoutedEventArgs e)
        {
            string content = "{\"deviceNo\":\"63594242\",\"messageType\":\"regist\"}";
            sock_mgr_.send_msg(content);
            return;
        }
    }
}
