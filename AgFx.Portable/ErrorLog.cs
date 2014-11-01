// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.


using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using PCLStorage;
using System.Threading.Tasks;

namespace AgFx {
    /// <summary>
    /// Helper for persisting unhandled exceptions to disk and being able to retrive them later.   
    /// </summary>
    public static class ErrorLog {
        private const string LogFile = "error.log";
        private const string Delimiter = "_\t_";

        /// <summary>
        /// Deletes the error log.
        /// </summary>
        public static async Task ClearAsync()
        {
            var localStorage = FileSystem.Current.LocalStorage;

            var fileExists = await localStorage.CheckExistsAsync(LogFile);
            if (fileExists == ExistenceCheckResult.FileExists)
            {
                var logFile = await localStorage.GetFileAsync(LogFile);
                if (logFile != null)
                {
                    await logFile.DeleteAsync();
                }
            }
        }

        /// <summary>
        /// Write an exception to disk.
        /// </summary>
        /// <param name="description"></param>
        /// <param name="ex"></param>
        public static async Task WriteErrorAsync(string description, Exception ex)
        {
            var localStorage = FileSystem.Current.LocalStorage;
            var file = await localStorage.CreateFileAsync(LogFile, CreationCollisionOption.OpenIfExists);

            var fileStream = await file.OpenAsync(FileAccess.ReadAndWrite); ;

            StreamWriter sw = new StreamWriter(fileStream);
            sw.WriteLine(DateTime.UtcNow);
            sw.WriteLine(description);
            sw.WriteLine(Delimiter);
            sw.WriteLine(ex);
            sw.WriteLine(Delimiter);
            sw.WriteLine(Delimiter);
            sw.Flush();
        }

        /// <summary>
        /// Get the list of exceptions
        /// </summary>
        /// <param name="clear">clear the list after retreival</param>
        /// <returns></returns>
        public static async Task<IEnumerable<ErrorEntry>> GetErrorsAsync(bool clear)
        {

            var localStorage = FileSystem.Current.LocalStorage;
            var file = await localStorage.CreateFileAsync(LogFile, CreationCollisionOption.OpenIfExists);

            var fileStream = await file.OpenAsync(FileAccess.ReadAndWrite);

            var fileExists = await localStorage.CheckExistsAsync(LogFile);

            StreamReader sr = new StreamReader(fileStream);


            List<ErrorEntry> entries = new List<ErrorEntry>();

            for (string ln = sr.ReadLine(); ln != null; ln = sr.ReadLine())
            {
                DateTime ts;
                if (DateTime.TryParse(ln, out ts))
                {
                    try
                    {
                        var currentErrorEntry = new ErrorEntry();
                        currentErrorEntry.Timestamp = ts;

                        currentErrorEntry.Description = ReadItem(sr, Delimiter);
                        currentErrorEntry.Exception = ReadItem(sr, Delimiter);

                        ln = sr.ReadLine();
                        Debug.Assert(ln == Delimiter, "Expected delimiter");
                        entries.Add(currentErrorEntry);
                    }
                    catch
                    {

                    }
                }
            }
            if (clear)
            {
                await ClearAsync();
            }
            return entries;
        }

        private static string ReadItem(StreamReader sr, string delimiter) {
            StringBuilder sb = new StringBuilder();
            for (
                string ln = sr.ReadLine();
                ln != null;
                ln = sr.ReadLine()) {
                sb.Append(ln);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Class describing an entry in the error log.
        /// </summary>
        public class ErrorEntry {
            /// <summary>
            /// The time of the error.
            /// </summary>
            public DateTime Timestamp {
                get;
                internal set;
            }

            /// <summary>
            /// A description
            /// </summary>
            public string Description {
                get;
                internal set;
            }

            /// <summary>
            /// The Exception that initiated the error
            /// </summary>
            public string Exception {
                get;
                internal set;
            }

            /// <summary>
            /// override
            /// </summary>
            /// <returns></returns>
            public override string ToString() {
                return String.Format("{0}: {1}\r\nStack trace:\r\n{2}\r\n\r\n", Timestamp, Description, Exception);
            }
        }
    }
}
