using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace Test.Touchsocket
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Do();
            Hert();
            Console.ReadLine();
        }
        public static FileLogger logger;
        public static TcpService service;

        public static void Do()
        {
            logger = new FileLogger();
            service = new TcpService();
            service.Connecting = (client, e) => { };//有客户端正在连接
            service.Connected = (client, e) => {
                logger.Info($"客户端ID{client.IP}:{client.Port}成功连接");
                client.Logger.Info($"客户端ID{client.IP}:{client.Port}成功连接");
            };//有客户端成功连接
            service.Disconnected = (client, e) => {
                logger.Info($"客户端ID{client.IP}:{client.Port}断开连接");
                client.Logger.Info($"客户端ID{client.IP}:{client.Port}断开连接");

            };//有客户端断开连接
            service.Received = (client, byteBlock, requestInfo) =>
            {
                //从客户端收到信息
                string mes = Encoding.UTF8.GetString(byteBlock.Buffer, 0, byteBlock.Len);
                try
                {
                    var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(mes);
                    if(dto == null)
                    {
                        logger.Info($"已从{client.IP}:{client.Port}接收的信息非Json,\r\n{mes}");
                        client.Logger.Info($"已从{client.IP}:{client.Port}接收的信息非Json,\r\n{mes}");

                        client.Send("");
                    }
                    else
                    {
                        logger.Info($"已从{client.IP}:{client.Port}接收到信息：{mes}");
                        client.Logger.Info($"已从{client.IP}:{client.Port}接收到信息：{mes}");
                    }
                }
                catch (Exception e)
                {
                    client.Logger.Info($"已从{client.IP}:{client.Port}接收的信息非Json,\r\n{mes}");
                    logger.Info($"已从{client.IP}:{client.Port}接收的信息非Json,\r\n{mes}");
                }
            };

            service.Setup(new TouchSocketConfig()//载入配置
                .SetListenIPHosts("tcp://127.0.0.1:43219", 43219)//同时监听两个地址
                .SetTcpDataHandlingAdapter(() => new JsonDataHandleAdapter())
                .ConfigureContainer(a =>
                {
                    a.AddConsoleLogger();//添加一个控制台日志注入（注意：在maui中控制台日志不可用）
                })
                .ConfigurePlugins(a =>
                {
                    //a.Add();//此处可以添加插件
                }))
                .Start();//启动
        }

        public static void Hert()
        {
            Task.Factory.StartNew(() => {
                // long running process
                do
                {
                    var allClient = service.GetClients();
                    foreach (var client in allClient)
                    {
                        client.Send("{\"code\":1001,\"serial\":1111}");
                    }
                    Thread.Sleep(20000);
                } while (true);
            }, TaskCreationOptions.LongRunning);
        }
    }
}
