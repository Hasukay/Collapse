﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI.Data
{
    public class HttpClientTool
    {
        readonly HttpClient httpClient;
        protected Stream localStream;
        protected Stream remoteStream;
        public event EventHandler<DownloadStatusChanged> ResumablityChanged;
        public event EventHandler<DownloadProgressChanged> ProgressChanged;
        public event EventHandler<DownloadProgressCompleted> Completed;

        public bool stop = true; // by default stop is true
        /* Declare download buffer
         * by default: 16 KiB (16384 bytes)
        */
        readonly long bufflength = 16384;

        public HttpClientTool()
        {
            httpClient = new(
            new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = true,
                MaxConnectionsPerServer = 32,
                AllowAutoRedirect = true
            });
        }

        DownloadStatusChanged resumabilityStatus;

        public bool DownloadFile(string input, string output, string customMessage = "", long startOffset = -1, long endOffset = -1, CancellationToken token = new CancellationToken())
        {
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading {Path.GetFileName(output)}";
            bool ret = true;

            while (!(ret = GetRemoteStreamResponse(input, output, startOffset, endOffset, customMessage, token, false)))
            {
                LogWriteLine($"Retrying...");
                Thread.Sleep(1000);
                ret = false;
            }

            return ret;
        }

        public bool DownloadStream(string input, MemoryStream output, CancellationToken token, long startOffset = -1, long endOffset = -1, string customMessage = "")
        {
            if (string.IsNullOrEmpty(customMessage))
                customMessage = $"Downloading to stream";
            bool ret = true;
            localStream = output;

            while (!(ret = GetRemoteStreamResponse(input, @"buffer", startOffset, endOffset, customMessage, token, true)))
            {
                LogWriteLine($"Retrying...");
                Thread.Sleep(1000);
                ret = false;
            }

            return ret;
        }

        bool GetRemoteStreamResponse(string input, string output, long startOffset, long endOffset, string customMessage, CancellationToken token, bool isStream)
        {
            bool returnValue = true;
            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = false });

            try
            {
                UseStream(input, output, startOffset, endOffset, customMessage, token, isStream);
            }
            catch (HttpRequestException e)
            {
                returnValue = ThrowWebExceptionAsBool(e);
            }
            catch (TaskCanceledException e)
            {
                returnValue = true;
                throw new TaskCanceledException(e.ToString());
            }
            catch (OperationCanceledException e)
            {
                returnValue = true;
                throw new TaskCanceledException(e.ToString());
            }
            catch (NullReferenceException e)
            {
                LogWriteLine($"This file {input} has 0 byte in size.\r\nTraceback: {e}", LogType.Error, true);
                returnValue = false;
            }
            catch (Exception e)
            {
                LogWriteLine($"An error occured while downloading {Path.GetFileName(output)}\r\nTraceback: {e}", LogType.Error, true);
                returnValue = false;
            }
            finally
            {
                if (!isStream) localStream?.Dispose();
                remoteStream?.Dispose();
            }

            OnCompleted(new DownloadProgressCompleted() { DownloadCompleted = true });

            return returnValue;
        }

        void UseStream(string input, string output, long startOffset, long endOffset, string customMessage, CancellationToken token, bool isStream)
        {
            token.ThrowIfCancellationRequested();
            long contentLength;
            FileInfo fileinfo = new(output);

            long existingLength = isStream ? localStream.Length : fileinfo.Exists ? fileinfo.Length : 0;

            HttpRequestMessage request = new(){ RequestUri = new Uri(input) };

            request.Headers.Range = (startOffset != -1 && endOffset != -1) ?
                                    new(startOffset, endOffset):
                                    new(existingLength, null);

            using HttpResponseMessage response = ThrowUnacceptableStatusCode(httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, token));

            contentLength = (startOffset != -1 && endOffset != -1) ?
                            endOffset - startOffset:
                            existingLength + (response.Content.Headers.ContentRange.Length - response.Content.Headers.ContentRange.From) ?? 0;

            resumabilityStatus = new((int)response.StatusCode == 206);

            if (!isStream)
                localStream = fileinfo.Open(resumabilityStatus.ResumeSupported ? FileMode.Append : FileMode.Create, FileAccess.Write);

            OnResumabilityChanged(resumabilityStatus);

            ReadRemoteStream(
                response,
                localStream,
                existingLength,
                contentLength,
                customMessage,
                token
                );
            response.Dispose();
        }

        HttpResponseMessage ThrowUnacceptableStatusCode(HttpResponseMessage input)
        {
            if (!input.IsSuccessStatusCode)
                throw new HttpRequestException($"an Error occured while doing request to {input.RequestMessage.RequestUri} with error code {(int)input.StatusCode} ({input.StatusCode})",
                    null,
                    input.StatusCode);

            return input;
        }

        void ReadRemoteStream(
           HttpResponseMessage response,
           Stream localStream,
           long existingLength,
           long contentLength,
           string customMessage,
           CancellationToken token)
        {
            int byteSize = 0;
            long totalReceived = byteSize + existingLength;
            byte[] buffer = new byte[bufflength];
            using (remoteStream = response.Content.ReadAsStream(token))
            {
                var sw = Stopwatch.StartNew();
                while ((byteSize = remoteStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    localStream.Write(buffer, 0, byteSize);
                    totalReceived += byteSize;

                    OnProgressChanged(new(totalReceived, contentLength, sw.Elapsed.TotalSeconds) { Message = customMessage, CurrentReceived = byteSize });
                }

                sw.Stop();
            }
        }

        bool ThrowWebExceptionAsBool(HttpRequestException e)
        {
            switch (GetStatusCodeResponse(e))
            {
                // Always ignore 416 code
                case 416:
                    return true;
                default:
                    LogWriteLine(e.Message, LogType.Error, true);
                    return false;
            }
        }

        short GetStatusCodeResponse(HttpRequestException e) => (short)e.StatusCode;

        protected virtual void OnResumabilityChanged(DownloadStatusChanged e) => ResumablityChanged?.Invoke(this, e);
        protected virtual void OnProgressChanged(DownloadProgressChanged e) => ProgressChanged?.Invoke(this, e);
        protected virtual void OnCompleted(DownloadProgressCompleted e) => Completed?.Invoke(this, e);
    }

    public class DownloadStatusChanged : EventArgs
    {
        public DownloadStatusChanged(bool canResume) => ResumeSupported = canResume;
        public bool ResumeSupported { get; private set; }
    }

    public class DownloadProgressCompleted : EventArgs
    {
        public bool DownloadCompleted { get; set; }
    }

    public class DownloadProgressChanged : EventArgs
    {
        public DownloadProgressChanged(long totalReceived, long fileSize, double totalSecond)
        {
            BytesReceived = totalReceived;
            TotalBytesToReceive = fileSize;
            CurrentSpeed = (long)(totalReceived / totalSecond);
        }
        public string Message { get; set; }
        public long CurrentReceived { get; set; }
        public long BytesReceived { get; private set; }
        public long TotalBytesToReceive { get; private set; }
        public float ProgressPercentage => ((float)BytesReceived / (float)TotalBytesToReceive) * 100;
        public long CurrentSpeed { get; private set; }
        public TimeSpan TimeLeft => TimeSpan.FromSeconds((TotalBytesToReceive - BytesReceived) / CurrentSpeed);
    }
}
