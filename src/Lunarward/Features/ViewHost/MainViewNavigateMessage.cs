<<<<<<< /tmp/_ours
using System;
||||||| /tmp/_base
﻿using System;
=======
﻿using Starward.Core.HoYoPlay;
using System;
>>>>>>> /tmp/_theirs

namespace Starward.Features.ViewHost;

internal record class MainViewNavigateMessage(Type Page, GameId? GameId = null);
