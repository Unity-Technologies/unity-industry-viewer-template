using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Unity.Industry.Viewer.Navigation.VR
{
    public class FindAndAttach : MonoBehaviour
    {
        [SerializeField]
        LazyFollow m_LazyFollow;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        IEnumerator Start()
        {
            var controller = GameObject.Find("Right Controller");
            while (controller == null)
            {
                yield return null;
                controller = GameObject.Find("Right Controller");
            }

            m_LazyFollow.target = controller.transform;
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
