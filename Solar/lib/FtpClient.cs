using System.Diagnostics;
using System.Data;
using System.Collections;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using System;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace Solar
{
    public class FTPclient
    {
        public FTPclient()
        {
        }

        public FTPclient(string Hostname)
        {
            _hostname = Hostname;
        }

        public FTPclient(string Hostname, string Username, string Password)
        {
            _hostname = Hostname;
            _username = Username;
            _password = Password;
        }

        public List<string> ListDirectory(string directory)
        {
            System.Net.FtpWebRequest ftp = GetRequest(GetDirectory(directory));
            ftp.Method = System.Net.WebRequestMethods.Ftp.ListDirectory;

            string str = GetStringResponse(ftp);
            str = str.Replace("\r\n", "\r").TrimEnd('\r');

            List<string> result = new List<string>();
            result.AddRange(str.Split('\r'));
            return result;
        }

        public FTPdirectory ListDirectoryDetail(string directory)
        {
            System.Net.FtpWebRequest ftp = GetRequest(GetDirectory(directory));
            ftp.Method = System.Net.WebRequestMethods.Ftp.ListDirectoryDetails;

            string str = GetStringResponse(ftp);
            str = str.Replace("\r\n", "\r").TrimEnd('\r');
            return new FTPdirectory(str, _lastDirectory);
        }

        public bool Upload(string localFilename, string targetFilename)
        {
            try
            {
                if (!File.Exists(localFilename))
                {
                    throw (new ApplicationException("File " + localFilename + " not found"));
                }
            }
            catch
            {
                return false;
            }

            FileInfo fi = new FileInfo(localFilename);
            return Upload(fi, targetFilename);
        }

        public bool Upload(FileInfo fi, string targetFilename)
        {
            bool fail = false;
            string target;
            if (targetFilename.Trim() == "")
            {
                target = this.CurrentDirectory + fi.Name;
            }
            else if (targetFilename.Contains("/"))
            {
                target = AdjustDir(targetFilename);
            }
            else
            {
                target = CurrentDirectory + targetFilename;
            }

            string URI = Hostname + target;
            System.Net.FtpWebRequest ftp = GetRequest(URI);

            ftp.Method = System.Net.WebRequestMethods.Ftp.UploadFile;
            ftp.UseBinary = true;

            ftp.ContentLength = fi.Length;

            const int BufferSize = 2048;
            byte[] content = new byte[BufferSize - 1 + 1];
            int dataRead;

            using (FileStream fs = fi.OpenRead())
            {
                try
                {
                    using (Stream rs = ftp.GetRequestStream())
                    {
                        do
                        {
                            dataRead = fs.Read(content, 0, BufferSize);
                            rs.Write(content, 0, dataRead);
                        } while (!(dataRead < BufferSize));
                        rs.Close();
                    }

                }
                catch (Exception)
                {
                    fail = true;
                }
                finally
                {
                    fs.Close();
                }

            }

            ftp = null;
            if (fail) return false;
            else
            {
                return true;
            }
        }

        public bool Download(string sourceFilename, string localFilename, bool PermitOverwrite)
        {
            FileInfo fi = new FileInfo(localFilename);
            return this.Download(sourceFilename, fi, PermitOverwrite);
        }

        public bool Download(FTPfileInfo file, string localFilename, bool PermitOverwrite)
        {
            return this.Download(file.FullName, localFilename, PermitOverwrite);
        }

        public bool Download(FTPfileInfo file, FileInfo localFI, bool PermitOverwrite)
        {
            return this.Download(file.FullName, localFI, PermitOverwrite);
        }

        public bool Download(string sourceFilename, FileInfo targetFI, bool PermitOverwrite)
        {
            if (targetFI.Exists && !(PermitOverwrite))
            {
                throw (new ApplicationException("Target file already exists"));
            }

            string target;
            if (sourceFilename.Trim() == "")
            {
                throw (new ApplicationException("File not specified"));
            }
            else if (sourceFilename.Contains("/"))
            {
                target = AdjustDir(sourceFilename);
            }
            else
            {
                target = CurrentDirectory + sourceFilename;
            }

            string URI = Hostname + target;

            System.Net.FtpWebRequest ftp = GetRequest(URI);
            ftp.Method = System.Net.WebRequestMethods.Ftp.DownloadFile;
            ftp.UseBinary = true;

            using (FtpWebResponse response = (FtpWebResponse)ftp.GetResponse())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (FileStream fs = targetFI.OpenWrite())
                    {
                        try
                        {
                            byte[] buffer = new byte[2048];
                            int read = 0;
                            do
                            {
                                read = responseStream.Read(buffer, 0, buffer.Length);
                                fs.Write(buffer, 0, read);
                            } while (!(read == 0));
                            responseStream.Close();
                            fs.Flush();
                            fs.Close();
                        }
                        catch (Exception)
                        {
                            fs.Close();
                            targetFI.Delete();
                            throw;
                        }
                    }

                    responseStream.Close();
                }

                response.Close();
            }


            return true;
        }

        public bool FtpDelete(string filename)
        {
            string URI = this.Hostname + GetFullPath(filename);

            System.Net.FtpWebRequest ftp = GetRequest(URI);
            ftp.Method = System.Net.WebRequestMethods.Ftp.DeleteFile;
            try
            {
                string str = GetStringResponse(ftp);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool FtpFileExists(string filename)
        {
            try
            {
                long size = GetFileSize(filename);
                return true;

            }
            catch (Exception ex)
            {
                if (ex is System.Net.WebException)
                {
                    if (ex.Message.Contains("550"))
                    {
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        public long GetFileSize(string filename)
        {
            string path;
            if (filename.Contains("/"))
            {
                path = AdjustDir(filename);
            }
            else
            {
                path = this.CurrentDirectory + filename;
            }
            string URI = this.Hostname + path;
            System.Net.FtpWebRequest ftp = GetRequest(URI);
            ftp.Method = System.Net.WebRequestMethods.Ftp.GetFileSize;
            string tmp = this.GetStringResponse(ftp);
            return GetSize(ftp);
        }

        public bool FtpRename(string sourceFilename, string newName)
        {
            string source = GetFullPath(sourceFilename);
            if (!FtpFileExists(source))
            {
                throw (new FileNotFoundException("File " + source + " not found"));
            }

            string target = GetFullPath(newName);
            if (target == source)
            {
                throw (new ApplicationException("Source and target are the same"));
            }
            else if (FtpFileExists(target))
            {
                throw (new ApplicationException("Target file " + target + " already exists"));
            }

            string URI = this.Hostname + source;

            System.Net.FtpWebRequest ftp = GetRequest(URI);
            ftp.Method = System.Net.WebRequestMethods.Ftp.Rename;
            ftp.RenameTo = target;

            try
            {
                string str = GetStringResponse(ftp);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool FtpCreateDirectory(string dirpath)
        {
            string URI = this.Hostname + AdjustDir(dirpath);
            System.Net.FtpWebRequest ftp = GetRequest(URI);
            ftp.Method = System.Net.WebRequestMethods.Ftp.MakeDirectory;

            try
            {
                string str = GetStringResponse(ftp);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool FtpDeleteDirectory(string dirpath)
        {
            string URI = this.Hostname + AdjustDir(dirpath);
            System.Net.FtpWebRequest ftp = GetRequest(URI);
            ftp.Method = System.Net.WebRequestMethods.Ftp.RemoveDirectory;

            try
            {
                string str = GetStringResponse(ftp);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private FtpWebRequest GetRequest(string URI)
        {
            FtpWebRequest result = (FtpWebRequest)FtpWebRequest.Create(URI);
            result.Credentials = GetCredentials();
            result.KeepAlive = false;

            return result;
        }

        private System.Net.ICredentials GetCredentials()
        {
            return new System.Net.NetworkCredential(Username, Password);
        }

        private string GetFullPath(string file)
        {
            if (file.Contains("/"))
            {
                return AdjustDir(file);
            }
            else
            {
                return this.CurrentDirectory + file;
            }
        }

        private string AdjustDir(string path)
        {
            return ((path.StartsWith("/")) ? "" : "/").ToString() + path;
        }

        private string GetDirectory(string directory)
        {
            string URI;
            if (directory == "")
            {
                URI = Hostname + this.CurrentDirectory;
                _lastDirectory = this.CurrentDirectory;
            }
            else
            {
                if (!directory.StartsWith("/"))
                {
                    throw (new ApplicationException("Directory should start with /"));
                }
                URI = this.Hostname + directory;
                _lastDirectory = directory;
            }
            return URI;
        }

        private string _lastDirectory = "";

        private string GetStringResponse(FtpWebRequest ftp)
        {
            string result = "";
            using (FtpWebResponse response = (FtpWebResponse)ftp.GetResponse())
            {
                long size = response.ContentLength;
                using (Stream datastream = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(datastream))
                    {
                        result = sr.ReadToEnd();
                        sr.Close();
                    }

                    datastream.Close();
                }

                response.Close();
            }

            return result;
        }

        private long GetSize(FtpWebRequest ftp)
        {
            long size;
            using (FtpWebResponse response = (FtpWebResponse)ftp.GetResponse())
            {
                size = response.ContentLength;
                response.Close();
            }

            return size;
        }

        private string _hostname;

        public string Hostname
        {
            get
            {
                if (_hostname.StartsWith("ftp://"))
                {
                    return _hostname;
                }
                else
                {
                    return "ftp://" + _hostname;
                }
            }
            set
            {
                _hostname = value;
            }
        }

        private string _username;
        public string Username
        {
            get
            {
                return (_username == "" ? "anonymous" : _username);
            }
            set
            {
                _username = value;
            }
        }

        private string _password;
        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                _password = value;
            }
        }

        private string _currentDirectory = "/";
        public string CurrentDirectory
        {
            get
            {
                return _currentDirectory + ((_currentDirectory.EndsWith("/")) ? "" : "/").ToString();
            }
            set
            {
                if (!value.StartsWith("/"))
                {
                    throw (new ApplicationException("Directory should start with /"));
                }
                _currentDirectory = value;
            }
        }
    }

	public class FTPfileInfo
	{
		public string FullName
		{
			get
			{
				return Path + Filename;
			}
		}
		public string Filename
		{
			get
			{
				return _filename;
			}
		}
		public string Path
		{
			get
			{
				return _path;
			}
		}
		public DirectoryEntryTypes FileType
		{
			get
			{
				return _fileType;
			}
		}
		public long Size
		{
			get
			{
				return _size;
			}
		}
		public DateTime FileDateTime
		{
			get
			{
				return _fileDateTime;
			}
		}
		public string Permission
		{
			get
			{
				return _permission;
			}
		}
		public string Extension
		{
			get
			{
				int i = this.Filename.LastIndexOf(".");
				if (i >= 0 && i <(this.Filename.Length - 1))
				{
					return this.Filename.Substring(i + 1);
				}
				else
				{
					return "";
				}
			}
		}
		public string NameOnly
		{
			get
			{
				int i = this.Filename.LastIndexOf(".");
				if (i > 0)
				{
					return this.Filename.Substring(0, i);
				}
				else
				{
					return this.Filename;
				}
			}
		}
		private string _filename;
		private string _path;
		private DirectoryEntryTypes _fileType;
		private long _size;
		private DateTime _fileDateTime;
		private string _permission;

		public enum DirectoryEntryTypes
		{
			File,
			Directory
		}
		
		public FTPfileInfo(string line, string path)
		{
			Match m = GetMatchingRegex(line);
			if (m == null)
			{
				throw (new ApplicationException("Unable to parse line: " + line));
			}
			else
			{
				_filename = m.Groups["name"].Value;
				_path = path;

                Int64.TryParse(m.Groups["size"].Value, out _size);

				_permission = m.Groups["permission"].Value;
				string _dir = m.Groups["dir"].Value;
				if (_dir != "" && _dir != "-")
				{
					_fileType = DirectoryEntryTypes.Directory;
				}
				else
				{
					_fileType = DirectoryEntryTypes.File;
				}
				
				try
				{
					_fileDateTime = DateTime.Parse(m.Groups["timestamp"].Value);
				}
				catch (Exception)
				{
					_fileDateTime = Convert.ToDateTime(null);
				}
				
			}
		}
		
		private Match GetMatchingRegex(string line)
		{
			Regex rx;
			Match m;
			for (int i = 0; i <= _ParseFormats.Length - 1; i++)
			{
				rx = new Regex(_ParseFormats[i]);
				m = rx.Match(line);
				if (m.Success)
				{
					return m;
				}
			}
			return null;
		}
		
		private static string[] _ParseFormats = new string[] { 
            "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\w+\\s+\\w+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{4})\\s+(?<name>.+)", 
            "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\d+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{4})\\s+(?<name>.+)", 
            "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\d+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{1,2}:\\d{2})\\s+(?<name>.+)", 
            "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})\\s+\\d+\\s+\\w+\\s+\\w+\\s+(?<size>\\d+)\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{1,2}:\\d{2})\\s+(?<name>.+)", 
            "(?<dir>[\\-d])(?<permission>([\\-r][\\-w][\\-xs]){3})(\\s+)(?<size>(\\d+))(\\s+)(?<ctbit>(\\w+\\s\\w+))(\\s+)(?<size2>(\\d+))\\s+(?<timestamp>\\w+\\s+\\d+\\s+\\d{2}:\\d{2})\\s+(?<name>.+)", 
            "(?<timestamp>\\d{2}\\-\\d{2}\\-\\d{2}\\s+\\d{2}:\\d{2}[Aa|Pp][mM])\\s+(?<dir>\\<\\w+\\>){0,1}(?<size>\\d+){0,1}\\s+(?<name>.+)" };
	}
	
	public class FTPdirectory : List<FTPfileInfo>
	{
		
		
		public FTPdirectory()
		{
			
		}
		
		public FTPdirectory(string dir, string path)
		{
			foreach (string line in dir.Replace("\n", "").Split(System.Convert.ToChar('\r')))
			{
				if (line != "")
				{
					this.Add(new FTPfileInfo(line, path));
				}
			}
		}
		
		public FTPdirectory GetFiles(string ext)
		{
			return this.GetFileOrDir(FTPfileInfo.DirectoryEntryTypes.File, ext);
		}
		
		public FTPdirectory GetDirectories()
		{
			return this.GetFileOrDir(FTPfileInfo.DirectoryEntryTypes.Directory, "");
		}
		
		private FTPdirectory GetFileOrDir(FTPfileInfo.DirectoryEntryTypes type, string ext)
		{
			FTPdirectory result = new FTPdirectory();
			foreach (FTPfileInfo fi in this)
			{
				if (fi.FileType == type)
				{
					if (ext == "")
					{
						result.Add(fi);
					}
					else if (ext == fi.Extension)
					{
						result.Add(fi);
					}
				}
			}
			return result;
			
		}
		
		public bool FileExists(string filename)
		{
			foreach (FTPfileInfo ftpfile in this)
			{
				if (ftpfile.Filename == filename)
				{
					return true;
				}
			}
			return false;
		}
		
		private const char slash = '/';
		
		public static string GetParentDirectory(string dir)
		{
			string tmp = dir.TrimEnd(slash);
			int i = tmp.LastIndexOf(slash);
			if (i > 0)
			{
				return tmp.Substring(0, i - 1);
			}
			else
			{
				throw (new ApplicationException("No parent for root"));
			}
		}
	}
}

