// Guids.cs
// MUST match guids.h
using System;

namespace reg.ext.vsSolutionBuildEvent
{
    static class GuidList
    {
        public const string guidvsSolutionBuildEventPkgString = "0482EC9B-51C8-4BC9-B128-AA6EF6022946";
        public const string guidvsSolutionBuildEventCmdSetString = "4B59ABE7-20F3-4C4A-870C-EB3B8B80CD2D";

        public static readonly Guid guidvsSolutionBuildEventCmdSet = new Guid(guidvsSolutionBuildEventCmdSetString);
    };
}