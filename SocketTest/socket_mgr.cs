using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketTest
{
    public class socket_mgr : notify_info
    {
        public socket_mgr()
        {
            finish_ = true;
            lock_ = new object();
            socket_ = new socket_base();
        }

        public bool begin(string _host, string _port)
        {
            if (!finish_)
            {
                return true;
            }

            host_ = _host;
            port_ = _port;

            finish_ = false;
            work_func(this);

            return true;
        }
        public void end()
        {
            if (finish_)
            {
                return;
            }
            finish_ = true;
            socket_.end();
        }
        public bool send_msg(string _msg)
        {
            if (!finish_ && null != socket_)
            {
                Debug.WriteLine("20、客户端发送消息给服务器:" + _msg.ToString());
                return socket_.send(_msg);
            }
            return false;
        }

        public object build(byte[] _buff, out uint _use)
        {
            // 把_buff 转换为msg
            _use = 0;
            if (_buff.Length < 4)
            {
                return null;
            }

            int len = 0;
            if (BitConverter.IsLittleEndian)
            {
                len = BitConverter.ToInt32(_buff.Take(4).Reverse().ToArray(), 0);
            }
            else
            {
                len = BitConverter.ToInt32(_buff, 0);
            }
            if (len + 4 > _buff.Length)
            {
                return null;
            }
            _use = (uint)len + 4;
            return Encoding.UTF8.GetString(_buff.Skip(4).Take(len).ToArray());
        }
        public byte[] serialize(object _msg)
        {
            // 把_msg 转换为byte[]
            var msg_s = _msg as string;
            if (null == msg_s)
            {
                return null;
            }

            byte[] r_byte = Encoding.UTF8.GetBytes(msg_s);
            if (null == r_byte)
            {
                return null;
            }

            byte[] buff = new byte[4 + r_byte.Length];
            if (BitConverter.IsLittleEndian)
            {
                System.Buffer.BlockCopy(BitConverter.GetBytes(r_byte.Length).Reverse().ToArray(), 0, buff, 0, 4);
            }
            else
            {
                System.Buffer.BlockCopy(BitConverter.GetBytes(r_byte.Length), 0, buff, 0, 4);
            }
            System.Buffer.BlockCopy(r_byte, 0, buff, 4, r_byte.Length);
            return buff;
        }
        public void on_close(socket_base _sock)
        {
            // 关闭
        }
        public void on_recv(socket_base _sock, object _msg)
        {
            // 收到消息
            lock (lock_)
            {
                last_recv_time_ = DateTime.Now;
                Debug.WriteLine("接收到服务器的消息:" + _msg.ToString());
            }
        }

        private void work_func(object _object)
        {
            Task.Run(async () => {
                Semaphore semap = new Semaphore(0, 1);
                while (!finish_)
                {
                    semap.WaitOne(1000);

                    if (socket_.is_finish())
                    {
                        await socket_.begin(host_, port_, this);
                        lock (lock_)
                        {
                            Debug.WriteLine("12、锁住的时间进程。。。。。。");
                            last_recv_time_ = DateTime.Now;
                            last_send_time_ = DateTime.Now;
                        }
                        continue;
                    }

                    bool re_connect_flg = false, send_keep_alive_flg_ = false;
                    lock (lock_)
                    {
                        Debug.WriteLine("13、锁住的重连进程。。。。。。");
                        if ((DateTime.Now - last_recv_time_).TotalSeconds > 100)
                        {
                            Debug.WriteLine("14、修改re_connect_flg为true");
                            re_connect_flg = true;
                        }
                        else if ((DateTime.Now - last_send_time_).TotalSeconds > 8)
                        {
                            Debug.WriteLine("15、修改send_keep_alive_flg_为true");
                            send_keep_alive_flg_ = true;
                        }
                    }

                    if (re_connect_flg)
                    {
                        socket_.end();
                    }
                    else if (send_keep_alive_flg_)
                    {
                        // 发送心跳信息
                        Debug.WriteLine("16、发送心跳消息。。。。");
                        socket_.send(new object());
                        last_send_time_ = DateTime.Now;
                    }
                }
            });
        }

        bool finish_;
        string host_;
        string port_;

        object lock_;
        DateTime last_recv_time_;
        DateTime last_send_time_;

        socket_base socket_;
    }
}
