using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace SocketTest
{
    public interface notify_info
    {
        void on_recv(socket_base _sock, object _msg);

        void on_close(socket_base _sock);

        /// 使用接受的数据构建消息，做粘包处理
        /// </summary>
        /// <param name="_buff">所有收到的数据</param>
        /// <param name="_use">本次构建包使用的字节数</param>
        /// <returns>构建好的消息对象</returns>
        object build(byte[] _buff, out uint _use);

        /// <summary>
        /// 根据需要发送的消息构建在网络上发送的数据
        /// </summary>
        /// <param name="_msg">需要发送的消息</param>
        /// <returns>网络发送的数据</returns>
        byte[] serialize(object _msg);
    }

    public class socket_base
    {
        public socket_base()
        {
            finish_ = true;
            lock_ = new object();
            seamp_ = new Semaphore(0, 1024);

            recv_buff_ = new List<byte>();

            send_msg_ = new List<object>();
        }

        public async Task<bool> begin(string _host, string _port, notify_info _notify)
        {
            if (!finish_)
            {
                return true;
            }

            notify_ = _notify;
            Debug.WriteLine("1、初始化streamsocket。");
            sock_ = new StreamSocket();
            HostName host = new HostName(_host);

            finish_ = false;
            try
            {
                await sock_.ConnectAsync(host, _port);
                Debug.WriteLine("2、streamsocket连接服务端。");
                Debug.WriteLine("3、初始化发送消息线程");
                await begin_send_func(this);
                Debug.WriteLine("3、完成发送消息线程");
                Debug.WriteLine("4、初始化接收消息线程");
                await begin_recv_func(this);
                Debug.WriteLine("4、完成接收消息线程");
            }
            catch
            {
                finish_ = true;
                return false;
            }

            return true;
        }
        public async void end()
        {
            if (finish_)
            {
                return;
            }

            seamp_.Release();
            await sock_.CancelIOAsync();
        }
        public bool send(object _msg)
        {
            if (finish_ || null == lock_)
            {
                Debug.WriteLine("21、客户端发消息时，finish的值为：" + finish_);
                return false;
            }

            lock (lock_)
            {
                Debug.WriteLine("22、客户端加入消息队列，finish的值为：" + finish_);
                send_msg_.Add(_msg);
                seamp_.Release();
            }
            return true;
        }
        public bool is_finish()
        {
            return finish_;
        }

        private static Task begin_send_func(socket_base _this)
        {
            return Task.Run(() => {
                _this.send_func();
            });
        }
        private static Task begin_recv_func(socket_base _this)
        {
            return Task.Run(() => {
                _this.recv_func();
            });
        }

        private async void recv_func()
        {
            try
            {
                Debug.WriteLine("9、初始化datareader");
                DataReader reader = new DataReader(sock_.InputStream);
                reader.InputStreamOptions = InputStreamOptions.Partial;
                reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                reader.ByteOrder = ByteOrder.BigEndian;
                await reader.LoadAsync(256);
                while (!finish_ && reader.UnconsumedBufferLength > 0)
                {
                    byte[] buff = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(buff);
                    if (null == notify_)
                    {
                        await reader.LoadAsync(256);
                        continue;
                    }
                    lock (lock_)
                    {
                        recv_buff_.AddRange(buff);
                        object msg = null;
                        do
                        {
                            uint use = 0;
                            Debug.WriteLine("10、发送的消息为：" + recv_buff_.ToString());
                            msg = notify_.build(recv_buff_.ToArray(), out use);
                            if (0 != use)
                            {
                                recv_buff_.RemoveRange(0, (int)use);
                            }
                            if (null != msg)
                            {
                                notify_.on_recv(this, msg);
                            }
                        } while (null != msg);
                    }
                    await reader.LoadAsync(256);
                }
                Debug.WriteLine("11、分离与数据读取关联的流。");
                reader.DetachStream();

                reset();
            }
            catch (Exception e)
            {
                reset();
            }
        }
        private async void send_func()
        {

            while (!finish_)
            {
                Debug.WriteLine("5、发送消息线程启动");
                seamp_.WaitOne();

                object msg = null;
                lock (lock_)
                {
                    if (send_msg_.Count > 0)
                    {
                        msg = send_msg_[0];
                        send_msg_.RemoveAt(0);
                    }
                }
                if (null == msg || null == notify_)
                {
                    continue;
                }

                byte[] buff = notify_.serialize(msg);
                if (null == buff)
                {
                    continue;
                }
                try
                {
                    using (DataWriter writer = new DataWriter(sock_.OutputStream))
                    {
                        writer.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8; //注意
                        writer.ByteOrder = ByteOrder.BigEndian;
                        Debug.WriteLine("6、在writer中写入buffer" + buff.ToString());
                        writer.WriteBytes(buff);
                        Debug.WriteLine("7、写入。。。。");
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        Debug.WriteLine("8、完成写入。。。。");

                        writer.DetachStream();
                    }
                }
                catch (Exception e)
                {
                    reset();
                }
            }
        }

        private void reset()
        {
            lock (lock_)
            {
                if (finish_)
                {
                    return;
                }
                finish_ = true;
                sock_.Dispose();
                recv_buff_.Clear();
                send_msg_.Clear();
                if (null != notify_)
                {
                    notify_.on_close(this);
                }
            }
        }

        bool finish_;
        object lock_;
        Semaphore seamp_;
        StreamSocket sock_;

        notify_info notify_;

        List<byte> recv_buff_;
        List<object> send_msg_;
    }
}
