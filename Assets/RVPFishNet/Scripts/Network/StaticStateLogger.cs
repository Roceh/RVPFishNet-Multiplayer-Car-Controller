using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RVP
{
    /// <summary>
    /// Basic logging for comparing state between client and server
    /// 
    /// If you want to analyse the result files you need to use WinMerge
    /// 
    /// Process is do a small test run of both the client/server (seperate editors)
    /// 
    /// You will have two files in Documents. fishnet_rvp_LOG_CLIENT.txt & fishnet_rvp_LOG_SERVER.txt
    /// 
    /// Open both files in notepad++ or something similar
    /// 
    /// Find the a reconcile point in the CLIENT text file (serach for "RECONCILE START") and delete all lines
    /// above that.
    /// 
    /// Get the tick number directly below the reconcile point, e.g. TICK: 204 and search for the same tick on the 
    /// SERVER text file and delete all lines above that TICK: 204 on the server text file.
    /// 
    /// Open WinMerge and do a compare with the two text files. You will see small errors here and there, but that is all
    /// hopefully.
    /// </summary>
    public static class StaticStateLogger
    {
        private static List<string> _logs = new List<string>();

        /// <summary>
        /// Logging function that is only compiled in if DEBUG_SYNC preprocessor is set in Project Settings->Player->Script Define Symbols.
        /// *NOTE* even the arguments are not evaluated if the preprocessor is not set
        /// </summary>
        /// <param name="log">string to log</param>
        [Conditional("DEBUG_SYNC")]
        public static void Log(string log)
        {
            _logs.Add(log);
        }

        /// <summary>
        /// Saves log data - it is only compiled in if DEBUG_SYNC preprocessor is set in Project Settings->Player->Script Define Symbols.
        /// *NOTE* even the arguments are not evaluated if the preprocessor is not set
        /// </summary>
        [Conditional("DEBUG_SYNC")]
        public static void Save(string path)
        {
            File.WriteAllLines(path, _logs);
        }
    }
}
