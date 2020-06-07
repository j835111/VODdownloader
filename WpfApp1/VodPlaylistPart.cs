using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class VodPlaylistPart
    {
        #region Constructors

        public VodPlaylistPart(double length, string remoteFile, string localFile,int fileNumber)
        {
            if (string.IsNullOrWhiteSpace(remoteFile))
            {
                throw new ArgumentNullException(nameof(remoteFile));
            }

            if (string.IsNullOrWhiteSpace(localFile))
            {
                throw new ArgumentNullException(nameof(localFile));
            }

            Length = length;
            RemoteFile = remoteFile;
            LocalFile = localFile;
            FileNumber = fileNumber;
        }

        #endregion Constructors

        #region Properties

        public string RemoteFile { get; }

        public string LocalFile { get; }

        public double Length { get; }

        public int FileNumber { get; }

        public bool Downloaded { get; set; } = false;

        #endregion Properties
    }
}
