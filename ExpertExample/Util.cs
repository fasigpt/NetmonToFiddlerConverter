using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpertExample
{
    public static class MyUtil
    {
        public static List<int> IndexOfSequence2(this byte[] buffer, byte[] pattern, int startIndex)
        {
           List<int> positions = new List<int>();
            int i = Array.IndexOf<byte>(buffer, pattern[0], startIndex);
            while (i >= 0 && i <= buffer.Length - pattern.Length)
            {
                byte[] segment = new byte[pattern.Length];
                Buffer.BlockCopy(buffer, i, segment, 0, pattern.Length);
                if (segment.SequenceEqual<byte>(pattern))
                    positions.Add(i);
                i = Array.IndexOf<byte>(buffer, pattern[0], i + pattern.Length);
            }
            return positions;
        }
    }
}
