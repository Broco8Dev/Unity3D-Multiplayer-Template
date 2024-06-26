using UnityEngine;

public class MoveCamera : MonoBehaviour {

    private GameObject player;
    public Server serv;

    void Update() {
        if (serv.connected)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            transform.position = player.transform.position;
        }
    }
}
