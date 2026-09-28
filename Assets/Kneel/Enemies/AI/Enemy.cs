using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [Header("Attack Data")]
    public float aggressionRange;
    public float attackRange;
    public float attackMoveSpeed;

    [Header("Idle Data")]
    public float idleTime;

    [Header("Move Data")]
    public float moveSpeed;
    public float chaseSpeed;
    public float turnSpeed;

    [SerializeField]
    private Transform[] patrolPoints;
    private int currentPatrolIndex;

    public Transform player { get; private set; }
    public Animator anim { get; private set; }
    public NavMeshAgent agent { get; private set; }
    public EnemyStateMachine stateMachine { get; private set; }

    protected virtual void Awake()
    {
        stateMachine = new EnemyStateMachine();

        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        player = GameObject.Find("Player").GetComponent<Transform>();
    }

    protected virtual void Start()
    {
        InitializePatrolPoints();
    }

    protected virtual void Update() { }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, aggressionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public void AnimationTrigger() => stateMachine.currentState.AnimationTrigger();

    public bool PlayerInAgressionRange() =>
        Vector3.Distance(transform.position, player.position) < aggressionRange;

    public bool PlayerInAttackRange() =>
        Vector3.Distance(transform.position, player.position) < attackRange;

    public Vector3 GetPatrolDestination()
    {
        currentPatrolIndex++;

        if (currentPatrolIndex >= patrolPoints.Length)
            currentPatrolIndex = 0;

        return patrolPoints[currentPatrolIndex].transform.position;
        ;
    }

    private void InitializePatrolPoints()
    {
        foreach (Transform t in patrolPoints)
            t.parent = null;
    }

    public Quaternion FaceTarget(Vector3 target)
    {
        Quaternion targetRotation = Quaternion.LookRotation(target - transform.position);

        Vector3 currentEulerAngles = transform.rotation.eulerAngles;

        float yRotation = Mathf.LerpAngle(
            currentEulerAngles.y,
            targetRotation.eulerAngles.y,
            turnSpeed * Time.deltaTime
        );

        return Quaternion.Euler(currentEulerAngles.x, yRotation, currentEulerAngles.z);
    }
}
