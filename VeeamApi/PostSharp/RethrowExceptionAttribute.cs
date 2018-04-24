using PostSharp.Aspects;
using PostSharp.Serialization;

namespace VeeamApi
{
    /// <summary>
    /// Replace Try / Catch wrapping
    /// Makes available the original exception out of the library
    /// </summary>
    [PSerializable]
    public class RethrowExceptionAttribute : OnExceptionAspect
    {
        public override void OnException(MethodExecutionArgs args)
        {
            args.FlowBehavior = FlowBehavior.RethrowException; //default behaviour
        }
    }
}
