using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TouchSocket.Core;

namespace Test.Touchsocket
{
    internal class JsonDataHandleAdapter : SingleStreamDataHandlingAdapter
    {
        public static int JsonTempSize = 20;
        public static FileLogger Logger = new FileLogger();
        public static ConsoleLogger CLogger = ConsoleLogger.Default;

        /// <summary>
        /// 临时包，此包仅当前实例储存
        /// </summary>
        private ByteBlock m_tempByteBlock;

        public override bool CanSendRequestInfo => false;

        public override bool CanSplicingSend => false;

        protected override void PreviewReceived(ByteBlock byteBlock)
        {
            //收到从原始流式数据
            if (this.m_tempByteBlock == null)//如果没有临时包，则直接分包。
            {
                var buffer = byteBlock.Buffer;
                var r = byteBlock.Len;
                this.SplitPackage(buffer, 0, r);
            }
            else
            {
                //将新字节块写入
                var r = byteBlock.Len;
                var currentStr = Encoding.UTF8.GetString(byteBlock.Buffer, 0, r);
                if (currentStr.StartsWith("{"))
                {
                    m_tempByteBlock = null;
                    this.SplitPackage(byteBlock.Buffer, 0, r);
                }
                else
                {
                    m_tempByteBlock.Write(byteBlock.Buffer, 0, r);
                    var buffer = m_tempByteBlock.Buffer;
                    r = m_tempByteBlock.Len;

                    var str = Encoding.UTF8.GetString(buffer, 0, r);
                    if (r > 1024 * JsonTempSize)
                    {
                        this.m_tempByteBlock = null;
                        Logger.Info($"清空缓存块,缓存过大{JsonTempSize}kb\r\n{str}");
                        return;
                    }
                    var result = this.SplitPackage(buffer, 0, r);
                    if (result)
                    {
                        //清空缓存块
                        this.m_tempByteBlock = null;
                        //Logger.Info("清空缓存块");
                        //CLogger.Info($"清空缓存块\r\n{str}");
                    }
                    else
                    {
                        //Logger.Info("累计缓存块");
                        //CLogger.Info($"累计缓存块\r\n{str}");
                    }
                }
            }
        }

        protected override void PreviewSend(byte[] buffer, int offset, int length)
        {
            if (length > 1024)//超长判断
            {
                var p = length / 1024 + 1;
                for (int i = 0; i < p; i++)
                {
                    using (var byteBlock = new ByteBlock())
                    {
                        if (i == p - 1)
                        {
                            byteBlock.Write(buffer, i * 1024, length % 1024);//再写数据
                            this.GoSend(byteBlock.Buffer, 0, byteBlock.Len);
                        }
                        else
                        {
                            byteBlock.Write(buffer, i * 1024, 1024);//再写数据
                            this.GoSend(byteBlock.Buffer, 0, byteBlock.Len);
                        }
                    }
                }
            }
            else
            {
                //从内存池申请内存块，因为此处数据绝不超过255，所以避免内存池碎片化，每次申请1K
                //ByteBlock byteBlock = new ByteBlock(dataLen+1);//实际写法。
                using (var byteBlock = new ByteBlock())
                {
                    byteBlock.Write(buffer, offset, length);//再写数据
                    this.GoSend(byteBlock.Buffer, 0, byteBlock.Len);
                }
            }
        }

        protected override void PreviewSend(IList<ArraySegment<byte>> transferBytes)
        {
            //base.PreviewReceived();
            //throw new NotImplementedException();
        }

        protected override void PreviewSend(IRequestInfo requestInfo)
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// 处理数据
        /// </summary>
        /// <param name="byteBlock"></param>
        private void PreviewHandle(ByteBlock byteBlock)
        {
            try
            {
                this.GoReceived(byteBlock, null);
            }
            finally
            {
                byteBlock.Dispose();//在框架里面将内存块释放
            }
        }

        /// <summary>
        /// 分解包
        /// </summary>
        /// <param name="dataBuffer"></param>
        /// <param name="index"></param>
        /// <param name="r"></param>
        private bool SplitPackage(byte[] dataBuffer, int index, int r)
        {
            var str = Encoding.UTF8.GetString(dataBuffer, index, r);

            var byteBlock = new ByteBlock(r);
            byteBlock.Write(dataBuffer, index, r);
            if (r % 1024 == 0)
            {
                //长度是1024则可能是分包了

                if (str[str.Length - 1] != '}' && !str.Contains("}{"))
                {
                    //最后一个字符不是结束符号，基本就可以认定这是不完整的包
                    this.m_tempByteBlock = byteBlock;
                    //Logger.Info($"添加缓存块:\r\n{str}");
                    //CLogger.Info($"添加缓存块:\r\n{str}");
                    return false;
                }
                else
                {
                    return HandleStr(str);
                }
            }
            else
            {
                return HandleStr(str);
            }
        }

        public bool HandleStr(string str)
        {
            if (str.Contains("}{"))
            {
                var strs = Regex.Split(str, "}{", RegexOptions.IgnoreCase);
                for (int i = 0; i < strs.Length; i++)
                {
                    var itemStr = strs[i];
                    if (i == 0)
                    {
                        itemStr = itemStr + "}";
                    }
                    else if (i == strs.Length - 1)
                    {
                        itemStr = "{" + itemStr;
                    }
                    else
                    {
                        itemStr = "{" + itemStr + "}";
                    }
                    JObject jobj = null;
                    try
                    {
                        jobj = SerializeConvert.FromJsonString<JObject>(itemStr);
                        if (jobj != null)
                        {
                            var subBuffer = Encoding.UTF8.GetBytes(itemStr);
                            var byteBlockSub = new ByteBlock(subBuffer.Length);
                            byteBlockSub.Write(subBuffer);
                            this.PreviewHandle(byteBlockSub);
                        }
                        else
                        {
                            Logger.Error($"解析失败：{itemStr},{i}/{strs.Length}");
                        }
                    }
                    catch (Exception e)
                    {
                        if (i == strs.Length - 1)
                        {
                            var buffer = Encoding.UTF8.GetBytes(itemStr);
                            var r = buffer.Length;
                            var byteBlock = new ByteBlock(r);
                            byteBlock.Write(buffer, 0, r);
                            this.m_tempByteBlock = byteBlock;
                            Logger.Debug($"解析等待下一轮：{itemStr}");
                        }
                        else
                        {
                            Logger.Error($"解析异常抛弃：{itemStr},{i}/{strs.Length}");
                            Logger.Error($"解析异常抛弃原文：{str}");
                        }
                    }
                }
            }
            else
            {
                JObject jobj = null;
                try
                {
                    jobj = SerializeConvert.FromJsonString<JObject>(str);
                }
                catch (Exception e)
                {
                    //Logger.Error($"包异常，包:\r\n{str}\r\n异常信息:\r\n{e.ToString()}");
                    //CLogger.Error($"包异常，包:\r\n{str}");
                }
                if (jobj != null)
                {
                    var subBuffer = Encoding.UTF8.GetBytes(str);
                    var byteBlockSub = new ByteBlock(subBuffer.Length);
                    byteBlockSub.Write(subBuffer);
                    this.PreviewHandle(byteBlockSub);
                    return true;
                }
            }
            return false;
        }

    }
}
