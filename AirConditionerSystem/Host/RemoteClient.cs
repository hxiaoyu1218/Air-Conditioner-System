﻿using log4net;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Host {
	class RemoteClient {
		private TcpClient tcpclient;
		private NetworkStream streamToClient;
		private const int BufferSize = 8192;
		private ILog LOGGER = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private System.Threading.Thread requestThread;
		private byte clientNum = 0;
		private ClientStatus clientStatus;
		private System.Timers.Timer heartBeatTimer;
		private Object readLock = new object();
		private Object writeLock = new object();
		private IHostServiceCallback callback;

		public byte ClientNum {
			get { return clientNum; }
			set {
				clientNum = value;
			}
		}
		public ClientStatus ClientStatus {
			get { return clientStatus; }
			set {
				clientStatus = value;
			}
		}

		public RemoteClient(TcpClient client, IHostServiceCallback callback) {
			this.tcpclient = client;
			LOGGER.InfoFormat("Client Connected! {0} < -- {1}",
				client.Client.LocalEndPoint, client.Client.RemoteEndPoint);
			streamToClient = client.GetStream();
			clientStatus = new ClientStatus();
			this.callback = callback;

			heartBeatTimer = new System.Timers.Timer(5000);
			heartBeatTimer.AutoReset = true;
			heartBeatTimer.Elapsed += this.heartBeat;

			requestThread = new System.Threading.Thread(run);
			requestThread.Start(callback);
		}

		private void run(object cb) {
			heartBeatTimer.Enabled = true;
			IHostServiceCallback callback = cb as IHostServiceCallback;
			try {
				while (true) {
					Common.Package request = null;
					lock (readLock) {
						request = Common.PackageHelper.GetRequest(streamToClient);
						LOGGER.InfoFormat("Receive package {0} from client {1}!", request.ToString(),
							clientNum == 0 ? tcpclient.Client.RemoteEndPoint.ToString() : clientNum.ToString());
					}
					Common.Package response = PackageHandler.Deal(this, request, callback);
					SendPackage(response);
				}
			} catch (IOException e) {
				LOGGER.WarnFormat("Client {0} stop run, maybe close!", this.ClientNum, e);
			} catch (System.Threading.ThreadAbortException) {
				LOGGER.WarnFormat("Client {0} has been aborted!", this.ClientNum);
			} finally {
				heartBeatTimer.Enabled = false;
				lock (this) {
					this.clientStatus.Speed = (int)ESpeed.Unauthorized;
					this.ClientStatus.RealSpeed = (int)ESpeed.NoWind;
				}
				if (streamToClient != null)
					streamToClient.Dispose();
				if (tcpclient != null)
					tcpclient.Close();
				callback.CloseClient(this.clientNum);
			}
		}

		private void updateCost() {
			if (this.clientStatus.Speed == (int)ESpeed.Unauthorized || this.clientStatus.Speed == (int)ESpeed.NoWind) return;

			this.clientStatus.Cost += Common.Constants.CostPrePower *
				(this.clientStatus.RealSpeed == 1 ? Common.Constants.LowSpeedPower :
				(this.clientStatus.RealSpeed == 2 ? Common.Constants.MidSpeedPower :
				(this.clientStatus.RealSpeed == 3 ? Common.Constants.HighSpeedPower : 0)));
			using (SqlConnection con = new SqlConnection(new SQLConnector().Builder.ConnectionString)) {
				con.Open();
				SqlCommand cmd = con.CreateCommand();
				cmd.CommandText = "update from dt_RoomIDCard set Cost = @a where RoomNum = @b";
				cmd.Parameters.Clear();
				cmd.Parameters.AddWithValue("@b", this.ClientNum);
				cmd.Parameters.AddWithValue("@a", this.clientStatus.Cost);
				int ln = cmd.ExecuteNonQuery();

				if (ln != 1) throw new Exception(String.Format("Unknow error when update cost into db! Room:{0}, Cost{1}", this.ClientNum, this.ClientStatus.Cost));

				LOGGER.InfoFormat("Update room {0} cost {1} into db!", this.ClientNum, this.ClientStatus.Cost);
			}
		}

		private void heartBeat(object source, System.Timers.ElapsedEventArgs e) {

			this.updateCost();

			Common.Package response1, response2;
			if (clientStatus.Speed == (int)ESpeed.Unauthorized) return;
			response1 = new Common.HostCostPackage(clientStatus.Cost);
			response2 = new Common.HostSpeedPackage((int)clientStatus.RealSpeed);
			SendPackage(response1);
			SendPackage(response2);
		}

		public void Abort() {
			if (streamToClient != null)
				this.streamToClient.Dispose();
			requestThread.Abort();
		}

		private void SendPackage(Common.Package package) {
			try {
				lock (writeLock) {
					byte[] bts = Common.PackageHelper.GetByte(package);
					streamToClient.Write(bts, 0, bts.Length);
					LOGGER.InfoFormat("Send package {0} to host {1}.", package.ToString(),
						clientNum == 0 ? tcpclient.Client.RemoteEndPoint.ToString() : clientNum.ToString());
					LOGGER.DebugFormat("Package: {0}", BitConverter.ToString(bts));
				}
			} catch (Exception) {
				LOGGER.ErrorFormat("Error when send package to client:{0}, abort client.", this.ClientNum);
				requestThread.Abort();
			}
		}

		public void SetTargetTemperature(float temperature) {
			this.clientStatus.TargetTemperature = temperature;
		}

		public void ReceiveHeartBeat(float temperature) {
			this.clientStatus.LastHeartBeat = DateTime.Now;
			this.clientStatus.NowTemperature = temperature;
		}

		public void ChangeMode(IHostServiceCallback cb) {
			var tmp = cb.GetDefaultWorkingState();
			Common.HostModePackage hostModePackage = new Common.HostModePackage(tmp.Item1, tmp.Item2);
			SendPackage(hostModePackage);
			LOGGER.InfoFormat("Host change mode package send mode:{0}, temperature:{1}!", tmp.Item1, tmp.Item2);
		}

		public void ChangeSpeed(int speed) {
			LOGGER.InfoFormat("Client {0} try to change speed from {1} to {2}", ClientNum, ClientStatus.Speed, speed);
			this.ClientStatus.Speed = speed;
			callback.
		}

		public void StopWind() {

		}
	}
}
