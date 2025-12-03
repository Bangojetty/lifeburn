using UnityEngine;

namespace TestingScene {
    public class TestingManager : MonoBehaviour {
        public Camera mainCam;
        public Vector3 mousePosInWorld;
    
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update() {
            Vector3 mousePosition = Input.mousePosition;
            Vector3 worldPosition = mainCam.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y));
            mousePosInWorld = new Vector3(worldPosition.x, worldPosition.y, 0);
            Debug.Log("mouse position is: " + mousePosition);
            Debug.Log("world position is: " + worldPosition);
        }
    }
}
