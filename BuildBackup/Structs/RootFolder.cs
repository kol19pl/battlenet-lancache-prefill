﻿namespace BuildBackup.Structs
{
    //TODO comment
    public class RootFolder : EnumBase<RootFolder>
    {
        public static readonly RootFolder data = new RootFolder("data");
        public static readonly RootFolder config = new RootFolder("config");
        public static readonly RootFolder patch = new RootFolder("patch");

        private RootFolder(string name) : base(name)
        {
        }
    }
}
