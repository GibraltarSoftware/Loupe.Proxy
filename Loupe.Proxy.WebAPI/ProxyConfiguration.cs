using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loupe.Proxy.WebAPI
{
    public class ProxyConfiguration
    {
        public ProxyConfiguration()
        {
            MaxFileSizeKB = 20 * 1024; //20MB is a safe upper bound the Agent shouldn't go over.
        }

        /// <summary>
        /// Maximum size in KB the proxy will accept from an agent for a single transfer.
        /// </summary>
        public int MaxFileSizeKB { get; set; }
    }
}
