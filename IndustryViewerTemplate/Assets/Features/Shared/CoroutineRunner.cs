using UnityEngine;
using System;
using System.Collections;

namespace Unity.Industry.Viewer.Shared
{
    public class CoroutineRunner : MonoBehaviour
    {
        public void RunCoroutine(IEnumerator coroutine, Action action)
        {
            StartCoroutine(RunAndInvoke(coroutine, action));
        }

        private IEnumerator RunAndInvoke(IEnumerator coroutine, Action action)
        {
            yield return StartCoroutine(coroutine);
            action?.Invoke();
            Destroy(gameObject);
        }
    }
}
