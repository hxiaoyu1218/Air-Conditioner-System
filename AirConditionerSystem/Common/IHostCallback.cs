﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common {
	interface IHostCallback {
		Response[] DealRequest(Request request);
		Response ChangeMode();
		// Response Close();
	}
}
