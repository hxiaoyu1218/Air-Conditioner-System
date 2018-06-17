﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Host {
	public enum ESpeed {
		Unauthorized = -1,
		NoWind = 0,
		Small,
		Mid,
		Large
	}
	class ClientStatus {
		int speed;
		float targetTemperature;
		float nowTemperature;
		float cost;
		DateTime lastHeartBeat;

		public ClientStatus() {
			this.speed = (int)ESpeed.Unauthorized;
			this.targetTemperature = this.nowTemperature = -1;
			this.cost = 0;
			this.lastHeartBeat = DateTime.Now;
		}

		public ClientStatus(int speed, float targetTemperature, float nowTemperature, float cost) {
			this.speed = speed;
			this.targetTemperature = targetTemperature;
			this.nowTemperature = nowTemperature;
			this.cost = cost;
			lastHeartBeat = DateTime.Now;
		}

		public int Speed { get => Interlocked.Exchange(ref speed, speed); set => Interlocked.Exchange(ref speed, value); }
		public float TargetTemperature { get => Interlocked.Exchange(ref targetTemperature, targetTemperature); set => Interlocked.Exchange(ref targetTemperature, value); }
		public float Cost { get => Interlocked.Exchange(ref cost, cost); set => Interlocked.Exchange(ref cost, value); }
		public float NowTemperature { get => Interlocked.Exchange(ref nowTemperature, nowTemperature); set => Interlocked.Exchange(ref nowTemperature, value); }
		public DateTime LastHeartBeat {
			get {
				return lastHeartBeat;
			}
			set {
				lock (this) {
					lastHeartBeat = value;
				}
			}
		}
	}
}