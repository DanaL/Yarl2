using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarl2
{
    internal class Player
    {
        public ushort Row { get; set; }
        public ushort Col { get; set; }
        public string Name { get; set; }
        public int MaxHP { get; set; }
        public int CurrHP { get; set; }

        public Player(string name, ushort row, ushort col)
        {
            Name = name;
            Row = row;
            Col = col;
            MaxHP = 20;
            CurrHP = 15;
        }
    }
}
