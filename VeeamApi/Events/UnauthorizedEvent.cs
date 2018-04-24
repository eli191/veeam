using System;
using System.Threading.Tasks;

namespace VeeamApi
{
    public class UnauthorizedEvent
    {
        public delegate Task OnUnauthorized(object sender, EventArgs e);
    }
}
