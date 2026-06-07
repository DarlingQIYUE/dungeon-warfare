using UnityEngine;

namespace DungeonWarfare
{
    /// <summary>
    /// A terrain block laid on the road. It blocks pathfinding (enemies route
    /// around it) and turns that cell into buildable ground — you can put a tower
    /// on top of it. No combat; just a gold cost. The blocking itself is tracked
    /// by the grid; this component is the visual + cost holder.
    /// </summary>
    public class Terrain : MonoBehaviour
    {
        [SerializeField] private int cost = 15;
        public int Cost => cost;
    }
}
