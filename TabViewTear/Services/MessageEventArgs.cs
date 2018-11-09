using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TabViewTear.Services
{
    /// <summary>
    /// Information about Message sent between Windows.
    /// </summary>
    public class MessageEventArgs: EventArgs
    {
        public int FromId { get; private set; }

        public int ToId { get; private set; }

        public string Message { get; private set; }

        /// <summary>
        /// Extra misc data, should be primitive or thread-safe type.
        /// </summary>
        public object Data { get; private set; }

        public MessageEventArgs(int from, int to, string message, object data = null)
        {
            FromId = from;
            ToId = to;
            Message = message;
            Data = data;
        }
    }
}
