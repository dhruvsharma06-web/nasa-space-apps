using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.UI.Tests.PlayMode
{
    public sealed class PrefabInteractionTests
    {
#if UNITY_EDITOR
        [Test]
        public void SavedDebriefButtonsInvokeRuntimeCallbacksExactlyOnce()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ASTRA/UI/Prefabs/DebriefPanel.prefab");
            Assert.That(asset, Is.Not.Null, "Generate or import the P1 shared UI prefabs before running this test.");
            var instance = Object.Instantiate(asset);
            try
            {
                var panel = instance.GetComponent<DebriefPanel>();
                var retryCount = 0;
                var hubCount = 0;
                panel.onRetry.AddListener(() => retryCount++);
                panel.onBackToHub.AddListener(() => hubCount++);

                instance.transform.Find("Retry").GetComponent<Button>().onClick.Invoke();
                instance.transform.Find("BackToHub").GetComponent<Button>().onClick.Invoke();

                Assert.That(retryCount, Is.EqualTo(1));
                Assert.That(hubCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
#endif
    }
}
