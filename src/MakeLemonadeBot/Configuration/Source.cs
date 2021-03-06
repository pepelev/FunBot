﻿using System;

namespace MakeLemonadeBot.Configuration
{
    public abstract class Source
    {
        public abstract string SpreadsheetId { get; }
        public abstract string Sheet { get; }

        public abstract class Collection
        {
            public abstract TimeSpan UpdatePeriod { get; }
            public abstract bool Contains(string name);
            public abstract Source Get(string name);
        }
    }
}