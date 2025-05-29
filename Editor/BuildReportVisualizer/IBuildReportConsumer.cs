#if UNITY_2022_2_OR_NEWER
namespace Bundles.Editor
{
    internal interface IBuildReportConsumer
    {
        void Consume(BuildLayout buildReport);
    }

}
#endif
