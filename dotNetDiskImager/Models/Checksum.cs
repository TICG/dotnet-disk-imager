﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dotNetDiskImager.Models
{
    public enum ChecksumType { MD5, SHA1 }

    public class Checksum
    {
        public delegate void ChecksumProgressChangedEventHandler(object sender, ChecksumProgressChangedEventArgs eventArgs);
        public delegate void ChecksumDoneEventHandler(object sender, ChecksumDoneEventArgs eventArgs);

        public event ChecksumProgressChangedEventHandler ChecksumProgressChanged;
        public event ChecksumDoneEventHandler ChecksumDone;

        volatile bool cancelPending = false;

        public void BeginChecksumCalculation(string imagePath, ChecksumType checksumType)
        {
            HashAlgorithm checksum = null;

            switch (checksumType)
            {
                case ChecksumType.MD5:
                    checksum = MD5.Create();
                    break;
                case ChecksumType.SHA1:
                    checksum = SHA1.Create();
                    break;
            }
            checksum.Initialize();

            new Thread(() =>
            {
                cancelPending = false;
                byte[] fileData = new byte[512 * 1024];
                long totalReaded = 0;
                int readed = 0;
                int percent = 0;
                int lastPercent = 0;

                using (FileStream fs = new FileStream(imagePath, FileMode.Open))
                {
                    while (totalReaded < fs.Length)
                    {
                        if (cancelPending)
                            break;

                        readed = fs.Read(fileData, 0, 512 * 1024);
                        totalReaded += readed;
                        if (totalReaded >= fs.Length)
                        {
                            checksum.TransformFinalBlock(fileData, 0, readed);
                            break;
                        }

                        checksum.TransformBlock(fileData, 0, readed, null, 0);
                        percent = (int)(totalReaded / (fs.Length / 100.0)) + 1;
                        if (lastPercent != percent)
                        {
                            lastPercent = percent;
                            ChecksumProgressChanged?.Invoke(typeof(Checksum), new ChecksumProgressChangedEventArgs(percent));
                        }
                    }

                    if (cancelPending)
                    {
                        ChecksumDone?.Invoke(typeof(Checksum), new ChecksumDoneEventArgs("", false));
                        return;
                    }

                    StringBuilder result = new StringBuilder(checksum.Hash.Length * 2);

                    for (int i = 0; i < checksum.Hash.Length; i++)
                    {
                        result.Append(checksum.Hash[i].ToString("x2"));
                    }

                    ChecksumDone?.Invoke(typeof(Checksum), new ChecksumDoneEventArgs(result.ToString(), true));
                }
            })
            { IsBackground = true }.Start();
        }

        public void Cancel()
        {
            cancelPending = true;
        }
    }

    public class ChecksumProgressChangedEventArgs
    {
        public int Progress { get; }

        public ChecksumProgressChangedEventArgs(int progress)
        {
            Progress = progress;
        }
    }

    public class ChecksumDoneEventArgs
    {
        public string Checksum { get; }
        public bool Finished { get; }

        public ChecksumDoneEventArgs(string checksum, bool finished)
        {
            Checksum = checksum;
            Finished = finished;
        }
    }
}
