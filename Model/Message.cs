using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Message
    {
        /// <summary>
        /// Initialization Vector
        /// </summary>
        public string IV { get; set; }

        /// <summary>
        /// Key for symmetric algorithm
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Text to be encrypted or decrypted
        /// </summary>
        public string Text { get; set; }
        
        /// <summary>
        /// Encryption algorithm name
        /// </summary>
        public string Alg { get; set; }
        
        /// <summary>
        /// Text should be encrypted
        /// true - encrypted, false - decrypted
        /// </summary>
        public bool Encrypt { get; set; }
    }
}
