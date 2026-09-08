using System;
using UnityEngine;

namespace Framework.Screen
{
    public class SafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform[] safeAreaRect = Array.Empty<RectTransform>();
        
        public void Apply()
        {
            
        }
    }
}