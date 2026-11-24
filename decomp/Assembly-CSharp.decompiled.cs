using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyVersion("0.0.0.0")]
public class SavingOptions : MonoBehaviour, ISaveable
{
	[Serializable]
	private struct SaveData
	{
		public int speedrun;

		public int mouseInvertedX;

		public int mouseInvertedY;
	}

	public static SavingOptions Instance;

	public int speedrun;

	public int mouseInvertedX;

	public int mouseInvertedY;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public object SaveState()
	{
		SaveData saveData = default(SaveData);
		saveData.speedrun = speedrun;
		saveData.mouseInvertedX = mouseInvertedX;
		saveData.mouseInvertedY = mouseInvertedY;
		return saveData;
	}

	public void LoadState(object state)
	{
		SaveData saveData = (SaveData)state;
		speedrun = saveData.speedrun;
		mouseInvertedX = saveData.mouseInvertedX;
		mouseInvertedY = saveData.mouseInvertedY;
	}
}
public class ScalePopup : MonoBehaviour
{
	public float timer = -5f;

	[SerializeField]
	public TMP_Text textLabel;

	[SerializeField]
	private UnityEngine.UI.Image textLabel1;

	public UnityEngine.Color c;

	public UnityEngine.Color c1;

	private void Awake()
	{
		c = textLabel.color;
		c1 = textLabel1.color;
		c.a = 0f;
		c1.a = 0f;
	}

	private void Update()
	{
		textLabel.color = c;
		textLabel1.color = c1;
		timer -= Time.deltaTime;
		if (timer < 0f)
		{
			if (c.a >= 0f)
			{
				c.a -= 4f * Time.deltaTime;
			}
			if (c1.a >= 0f)
			{
				c1.a -= 2f * Time.deltaTime;
			}
		}
	}
}
public class AchievementPopup : MonoBehaviour
{
	[SerializeField]
	private GameObject initPos;

	[SerializeField]
	private GameObject upPos;

	[SerializeField]
	private TMP_Text textLabel;

	private float timer;

	private Vector3 buttonVelocity = Vector3.zero;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		textLabel.text = gameManager.lastAchievement;
		if (gameManager.popup)
		{
			gameManager.popup = false;
			timer = 10f;
		}
		if (timer > 0f && base.transform.position.y < upPos.transform.position.y)
		{
			base.transform.position += new Vector3(0f, 130f, 0f) * Time.deltaTime;
		}
		if (timer < 0f && base.transform.position.y > initPos.transform.position.y)
		{
			base.transform.position -= new Vector3(0f, 130f, 0f) * Time.deltaTime;
		}
	}
}
public class SecretObjectDisplay : MonoBehaviour
{
	public int secretId;

	public new string name;

	public bool collected;

	public Material masked;

	public Material displayed;

	public Vector3 rotation;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (gameManager.secretState[secretId] == 1)
		{
			collected = true;
		}
		else
		{
			collected = false;
		}
		if (collected)
		{
			base.transform.Rotate(rotation.x * Time.deltaTime, rotation.y * Time.deltaTime, rotation.z * Time.deltaTime);
			GetComponent<Renderer>().material = displayed;
		}
		else
		{
			GetComponent<Renderer>().material = masked;
		}
	}
}
public class SecretPopup : MonoBehaviour
{
	private float timer = -5f;

	[SerializeField]
	private TMP_Text textLabel;

	[SerializeField]
	private TMP_Text textLabel1;

	private UnityEngine.Color c;

	private UnityEngine.Color c1;

	private GameManager gameManager;

	private void Awake()
	{
		c = textLabel.color;
		c1 = textLabel1.color;
		c.a = 0f;
		c1.a = 0f;
	}

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		textLabel.color = c;
		textLabel1.color = c1;
		timer -= Time.deltaTime;
		if (gameManager.secretPopup)
		{
			c.a = 1f;
			c1.a = 1f;
			timer = 3f;
			gameManager.secretPopup = false;
		}
		if (timer < 0f)
		{
			if (c.a >= 0f)
			{
				c.a -= 2f * Time.deltaTime;
			}
			if (c1.a >= 0f)
			{
				c1.a -= 2f * Time.deltaTime;
			}
		}
	}
}
public class EnemyAiTutorial : MonoBehaviour
{
	public int enemyId;

	public NavMeshAgent agent;

	public Transform player;

	public LayerMask whatIsGround;

	public LayerMask whatIsPlayer;

	public Vector3 walkPoint;

	private bool walkPointSet;

	public float walkPointRange;

	public float timeBetweenAttacks;

	private bool alreadyAttacked;

	public GameObject projectile;

	public float sightRange;

	public float attackRange;

	public bool playerInSightRange;

	public bool playerInAttackRange;

	private void Awake()
	{
		player = GameObject.Find("Player").transform;
		agent = GetComponent<NavMeshAgent>();
	}

	private void Update()
	{
		playerInSightRange = Physics.CheckSphere(base.transform.position, sightRange, whatIsPlayer);
		playerInAttackRange = Physics.CheckSphere(base.transform.position, attackRange, whatIsPlayer);
		if (!playerInSightRange && !playerInAttackRange)
		{
			Patroling();
		}
		else if (playerInSightRange && !playerInAttackRange)
		{
			ChasePlayer();
		}
		else if (playerInAttackRange && playerInSightRange && enemyId == 0)
		{
			AttackPlayer();
		}
	}

	private void Patroling()
	{
		if (!walkPointSet)
		{
			SearchWalkPoint();
		}
		if (walkPointSet)
		{
			agent.SetDestination(walkPoint);
		}
		if ((base.transform.position - walkPoint).magnitude < 1f)
		{
			walkPointSet = false;
		}
	}

	private void SearchWalkPoint()
	{
		float num = UnityEngine.Random.Range(0f - walkPointRange, walkPointRange);
		float num2 = UnityEngine.Random.Range(0f - walkPointRange, walkPointRange);
		walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
		if (Physics.Raycast(walkPoint, -base.transform.up, 2f, whatIsGround))
		{
			walkPointSet = true;
		}
	}

	private void ChasePlayer()
	{
		agent.SetDestination(player.position);
		base.transform.LookAt(GameObject.Find("Player").transform);
	}

	private void AttackPlayer()
	{
		base.transform.LookAt(GameObject.Find("Player").transform);
		agent.SetDestination(player.transform.position);
		if (!alreadyAttacked)
		{
			agent.SetDestination(base.transform.position);
			Rigidbody component = UnityEngine.Object.Instantiate(projectile, GameObject.Find("ProjectilePos").transform.position, Quaternion.identity).GetComponent<Rigidbody>();
			component.GetComponent<Rigidbody>().AddForce(base.transform.forward * 15f, ForceMode.Impulse);
			component.GetComponent<Rigidbody>().AddForce(base.transform.up * 2f, ForceMode.Impulse);
			alreadyAttacked = true;
			Invoke("ResetAttack", timeBetweenAttacks);
		}
	}

	private void ResetAttack()
	{
		alreadyAttacked = false;
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = UnityEngine.Color.red;
		Gizmos.DrawWireSphere(base.transform.position, attackRange);
		Gizmos.color = UnityEngine.Color.yellow;
		Gizmos.DrawWireSphere(base.transform.position, sightRange);
	}
}
public class FacePlayer : MonoBehaviour
{
	private GameObject player;

	[SerializeField]
	private float distanceFromPlayer;

	[SerializeField]
	private float maxDistance = 7f;

	[SerializeField]
	private float damping = 3f;

	[SerializeField]
	private float minXRot = -35f;

	[SerializeField]
	private float maxXRot = 35f;

	[SerializeField]
	private float minYRot = -50f;

	[SerializeField]
	private float maxYRot = 35f;

	[SerializeField]
	private bool intervals;

	[SerializeField]
	private bool weirdNegative;

	[SerializeField]
	private Vector2 negativeInterval;

	[SerializeField]
	private Vector2 positiveInterval;

	[SerializeField]
	private Quaternion defaultRot;

	[SerializeField]
	private Quaternion facePlayerRot;

	[SerializeField]
	private Quaternion debug;

	[SerializeField]
	private Vector3 rotation;

	private void Awake()
	{
		player = GameObject.Find("Main Camera");
	}

	private void Start()
	{
		defaultRot = base.transform.rotation;
	}

	private void Update()
	{
		distanceFromPlayer = Vector3.Distance(base.transform.position, player.transform.position);
		rotation = player.transform.position - base.transform.position;
		facePlayerRot = Quaternion.LookRotation(rotation);
		debug = Quaternion.Euler(facePlayerRot.eulerAngles.x, facePlayerRot.eulerAngles.y, facePlayerRot.eulerAngles.z);
		if (distanceFromPlayer < maxDistance)
		{
			base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.Euler(facePlayerRot.eulerAngles.x, facePlayerRot.eulerAngles.y, facePlayerRot.eulerAngles.z), Time.deltaTime * damping);
		}
		else
		{
			base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.Euler(defaultRot.eulerAngles.x, defaultRot.eulerAngles.y, defaultRot.eulerAngles.z), Time.deltaTime * damping);
		}
	}

	private void LimitRot()
	{
		Vector3 eulerAngles = base.transform.rotation.eulerAngles;
		eulerAngles.x = ((eulerAngles.x > 180f) ? (eulerAngles.x - 360f) : eulerAngles.x);
		eulerAngles.x = Mathf.Clamp(eulerAngles.x, minXRot, maxXRot);
		if (intervals)
		{
			eulerAngles.y = ((eulerAngles.y < 0f) ? Mathf.Clamp(eulerAngles.y, -360f, negativeInterval.x) : ((eulerAngles.y > 0f) ? Mathf.Clamp(eulerAngles.y, positiveInterval.x, 360f) : 180f));
		}
		if (weirdNegative)
		{
			eulerAngles.y = Mathf.Clamp(eulerAngles.y, positiveInterval.x, negativeInterval.x);
		}
		else
		{
			eulerAngles.y = ((eulerAngles.y > 180f) ? (eulerAngles.y - 360f) : eulerAngles.y);
			eulerAngles.y = Mathf.Clamp(eulerAngles.y, minYRot, maxYRot);
		}
		base.transform.rotation = Quaternion.Euler(eulerAngles);
	}
}
public class Pigeon : MonoBehaviour
{
	[SerializeField]
	private float distanceFromPlayer;

	[SerializeField]
	private float maxDistance = 5f;

	[SerializeField]
	private float flyTime = 20f;

	[SerializeField]
	private float flySpeed = 2f;

	[SerializeField]
	private float verticalMovement = 1f;

	[SerializeField]
	private float flyTimer;

	[SerializeField]
	private bool randomRot = true;

	[SerializeField]
	private bool animate = true;

	[SerializeField]
	private Animator anim;

	private GameManager gameManager;

	private float walkTimer;

	private bool fly;

	private bool walk;

	private Vector3 initPos;

	private Vector3 flyDir;

	private Vector3 rotation;

	private GameObject player;

	[SerializeField]
	private float jumpTimer = 3f;

	private float value;

	[SerializeField]
	private AudioClip flyClip;

	private void Awake()
	{
		player = GameObject.Find("Player");
	}

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
		jumpTimer = UnityEngine.Random.Range(1f, 2f);
		if (randomRot)
		{
			base.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
		}
		initPos = base.transform.position;
	}

	private void Update()
	{
		distanceFromPlayer = Vector3.Distance(base.transform.position, player.transform.position);
		flyTimer -= Time.deltaTime;
		walkTimer = Mathf.Sin(Time.time) * (float)UnityEngine.Random.Range(1, 1);
		anim.SetBool("fly", fly);
		if (distanceFromPlayer < ((gameManager.trumpet == 1 && Input.GetButton("Fire1")) ? (maxDistance * 3f) : maxDistance) && !fly)
		{
			SoundManager.Instance.PlaySound(flyClip);
			flyDir = -(GameObject.Find("Player").transform.position - base.transform.position).normalized;
			base.transform.rotation = Quaternion.LookRotation(flyDir);
			fly = true;
			flyTimer = flyTime;
		}
		if (fly)
		{
			base.transform.position += new Vector3(flyDir.x * Time.deltaTime * flySpeed, verticalMovement * Time.deltaTime, flyDir.z * Time.deltaTime * flySpeed);
		}
		else if (animate)
		{
			jumpTimer -= Time.deltaTime;
			if (jumpTimer < 0.1f && jumpTimer > 0f)
			{
				value = UnityEngine.Random.Range(-1, 1);
				jumpTimer = 0f;
			}
			if (jumpTimer < 0f)
			{
				base.transform.Rotate(0f, (value > 0f) ? (290f * Time.deltaTime) : (-290f * Time.deltaTime), 0f);
				if (jumpTimer > -0.2f)
				{
					base.transform.position += new Vector3(0f, 2f * Time.deltaTime, 0f);
				}
				else if (jumpTimer > -0.35f)
				{
					base.transform.position -= new Vector3(0f, 2f * Time.deltaTime, 0f);
				}
				else
				{
					jumpTimer = UnityEngine.Random.Range(2f, 8f);
				}
			}
		}
		if (flyTimer < 0f && Vector3.Distance(initPos, player.transform.position) > 20f)
		{
			fly = false;
			base.transform.position = initPos;
		}
	}

	private void OnCollisionEnter(Collision col)
	{
		if ((col.collider.gameObject.layer == 7 || col.collider.gameObject.layer == 11) && !fly)
		{
			SoundManager.Instance.PlaySound(flyClip);
			flyDir = -(GameObject.Find("Player").transform.position - base.transform.position).normalized;
			base.transform.rotation = Quaternion.LookRotation(flyDir);
			fly = true;
			flyTimer = flyTime;
		}
	}
}
public class ProjectileSelfDestroy : MonoBehaviour
{
	private void Start()
	{
		UnityEngine.Object.Destroy(base.gameObject, 5f);
	}
}
public class Target : MonoBehaviour
{
	public float health = 50f;

	public float damage = 6f;

	public void TakeDamage(float amount)
	{
		health -= amount;
		if (health <= 0f)
		{
			Death();
		}
	}

	private void Start()
	{
	}

	private void Update()
	{
	}

	private void Death()
	{
		UnityEngine.Object.Destroy(base.gameObject);
	}
}
public class Ball : MonoBehaviour
{
	[SerializeField]
	private AudioClip ball;

	[SerializeField]
	private AudioSource ballSound;

	private float timer;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		timer -= Time.deltaTime;
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.collider.tag == "Dog")
		{
			if (!gameManager.playDog)
			{
				gameManager.Popup();
			}
			gameManager.playDog = true;
			gameManager.playDogState = 1;
			gameManager.lastAchievement = "Doggo friendly";
			SteamIntegration.Instance.UnlockAchivement("DoggoFriendly");
		}
		if (GetComponent<Rigidbody>().velocity.magnitude > 2f)
		{
			ballSound.PlayOneShot(ball);
		}
		timer = 0.8f;
	}

	private void OnTriggerEnter(Collider col)
	{
		if (col.gameObject.layer == 16)
		{
			GetComponent<Rigidbody>().AddForce(GameObject.Find("Player").GetComponent<FirstPersonController>().playerCamera.transform.forward * 4f, ForceMode.Impulse);
		}
	}
}
public class ChangeFirstSelectedButton : MonoBehaviour
{
	private void Start()
	{
	}

	private void Update()
	{
	}

	public void ChangeButton(GameObject button)
	{
		if (Input.GetJoystickNames().Length != 0 && Input.GetJoystickNames()[0] != "")
		{
			EventSystem current = EventSystem.current;
			current.SetSelectedGameObject(button, new BaseEventData(current));
		}
	}
}
public class ClubSound : MonoBehaviour
{
	private AudioSource audio;

	private GameManager gameManager;

	private void Start()
	{
		audio = GetComponent<AudioSource>();
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (gameManager.inClub)
		{
			audio.volume = Mathf.Lerp(audio.volume, GameObject.Find("AmbientSource").GetComponent<AudioSource>().volume, 1f * Time.deltaTime);
		}
		else if (!gameManager.inClub)
		{
			audio.volume = Mathf.Lerp(audio.volume, 0f, 3f * Time.deltaTime);
		}
	}
}
public class ContinueDialogueVisual : MonoBehaviour
{
	public TextMeshProUGUI tmp;

	public float glowTimer;

	public float glowSpeed;

	public float glowAmount;

	private void Start()
	{
	}

	private void Update()
	{
		glowTimer += Time.deltaTime * glowSpeed;
		tmp.fontMaterial.SetFloat(ShaderUtilities.ID_LightAngle, 3f + Mathf.Sin(glowTimer) * glowAmount);
	}
}
public class Arrow : MonoBehaviour
{
	public GameObject parent;

	public GameObject defaultPos;

	private Rigidbody rb;

	public Vector3 newVector;

	private bool hit;

	[SerializeField]
	private float impulsionForce;

	[SerializeField]
	private float horizontalForce;

	[SerializeField]
	private float arrowSpeed;

	[SerializeField]
	private float deceleration;

	public bool onWall;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		parent = base.transform.parent.gameObject;
		defaultPos = parent.GetComponent<Grabber>().arrowInitPosition;
	}

	private void Update()
	{
		if (!parent.GetComponent<InteractObject>().inHands)
		{
			return;
		}
		if (base.transform.parent != null)
		{
			parent = base.transform.parent.gameObject;
			defaultPos = parent.GetComponent<Grabber>().arrowInitPosition;
			base.transform.parent = parent.transform;
			rb.isKinematic = true;
			base.transform.position = defaultPos.transform.position;
			if (Input.GetButtonDown("Fire1"))
			{
				hit = true;
				base.transform.parent = null;
				rb.isKinematic = false;
				rb.AddForce(GameObject.Find("Player").GetComponent<FirstPersonController>().playerCamera.transform.forward * arrowSpeed);
			}
		}
		else if (base.transform.parent == null)
		{
			rb.isKinematic = false;
			if (Input.GetButtonDown("Fire1"))
			{
				rb.constraints = RigidbodyConstraints.None;
				base.transform.position = defaultPos.transform.position;
				base.transform.parent = parent.transform;
				onWall = false;
			}
		}
	}

	private void OnCollisionStay(Collision collision)
	{
		if (base.transform.parent == null && parent.GetComponent<InteractObject>().inHands && hit)
		{
			rb.constraints = RigidbodyConstraints.FreezeAll;
			newVector = (GameObject.Find("Player").transform.position - base.transform.position).normalized;
			onWall = true;
			hit = false;
			GameObject.Find("Player").GetComponent<FirstPersonController>().AddVerticalForce(new Vector3(0f, 0f - newVector.y, 0f), impulsionForce);
			GameObject.Find("Player").GetComponent<FirstPersonController>().AddHorizontalForce(new Vector3(0f - newVector.x, 0f, 0f - newVector.z), impulsionForce);
			GameObject.Find("Player").GetComponent<FirstPersonController>().deceleration = deceleration;
		}
	}
}
public class Blower : MonoBehaviour
{
	public Transform origin;

	private GameObject player;

	public float power = 0.2f;

	public bool isActive;

	private bool canfly;

	private float timer;

	[SerializeField]
	private float battery;

	[SerializeField]
	private float batteryMax = 2f;

	public AudioClip motorClip;

	public AudioClip endClip;

	public Vector3 blowMovement;

	private void Awake()
	{
		player = GameObject.Find("Player");
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (player.GetComponent<FirstPersonController>().characterController.isGrounded)
		{
			battery = batteryMax;
		}
		if (Input.GetButtonDown("Fire1") && GetComponent<InteractObject>().inHands && battery > 0f && !player.GetComponent<FirstPersonController>().pause && !GameObject.Find("Player").GetComponent<FirstPersonController>().pause)
		{
			SoundManager.Instance.PlayContinuousSound(motorClip);
		}
		if (Input.GetButton("Fire1") && GetComponent<InteractObject>().inHands && !player.GetComponent<FirstPersonController>().pause)
		{
			timer = 0.05f;
			isActive = true;
			battery -= Time.deltaTime;
			if (battery > 0f)
			{
				if (Physics.Raycast(player.transform.position, player.GetComponent<FirstPersonController>().playerCamera.transform.forward, out var hitInfo, 3f) && player.GetComponent<FirstPersonController>().characterController.isGrounded && Vector3.Angle(hitInfo.normal, Vector3.up) < player.GetComponent<FirstPersonController>().characterController.slopeLimit)
				{
					player.GetComponent<FirstPersonController>().moveDirection.y = 4f;
				}
				blowMovement += -player.GetComponent<FirstPersonController>().playerCamera.transform.forward * power / 4f * Time.deltaTime;
				player.GetComponent<FirstPersonController>().moveDirection += -player.GetComponent<FirstPersonController>().playerCamera.transform.forward * power * Time.deltaTime;
			}
			else
			{
				blowMovement = new Vector3(0f, 0f, 0f);
			}
		}
		if ((Input.GetButtonUp("Fire1") && GetComponent<InteractObject>().inHands && battery > 0f) || (battery < 0f && battery > -0.1f))
		{
			SoundManager.Instance.StopSound();
			if (!GameObject.Find("Player").GetComponent<FirstPersonController>().pause)
			{
				SoundManager.Instance.PlaySound(endClip);
			}
		}
		if (GetComponent<InteractObject>().inHands && !Input.GetButton("Fire1"))
		{
			blowMovement = new Vector3(0f, 0f, 0f);
			isActive = false;
		}
		else if (!GetComponent<InteractObject>().inHands)
		{
			blowMovement = new Vector3(0f, 0f, 0f);
			isActive = false;
		}
	}
}
public class Climber : MonoBehaviour
{
	private GameObject player;

	private float timer;

	[SerializeField]
	private float launchForce;

	[SerializeField]
	private float climberForce;

	private float mousey;

	public AudioClip hold;

	public AudioClip hit;

	public Vector3 pickPos;

	public Quaternion pickRot;

	public bool trigger;

	public bool pick;

	public bool touch;

	private bool hitb;

	private void Awake()
	{
		player = GameObject.Find("Player");
	}

	private void Start()
	{
	}

	private void Update()
	{
		mousey = 0f - Mathf.Clamp(Input.GetAxis("Mouse Y"), -2f, 2f);
		timer -= Time.deltaTime;
		if (Input.GetButtonDown("Jump") && trigger)
		{
			pick = false;
			timer = 0.2f;
			trigger = false;
			player.GetComponent<FirstPersonController>().AddForce(player.transform.right / 15f * player.GetComponent<FirstPersonController>().currentInputRaw.y, launchForce);
			player.GetComponent<FirstPersonController>().moveDirection.y = player.GetComponent<FirstPersonController>().jumpForce * climberForce * ((mousey > 1f) ? mousey : 1f);
		}
		if (!Input.GetButton("Fire1") && !player.GetComponent<FirstPersonController>().pause)
		{
			trigger = false;
		}
		if (Input.GetButtonDown("Fire1") && !player.GetComponent<FirstPersonController>().pause)
		{
			timer = 0f;
			hitb = true;
			pick = true;
		}
		if (GetComponent<InteractObject>().extended && timer > 0f && timer < 0.05f)
		{
			hitb = true;
		}
	}

	private void OnTriggerEnter(Collider collisionInfo)
	{
		touch = true;
		pickPos = base.transform.position;
		pickRot = base.transform.rotation;
	}

	private void OnTriggerStay(Collider collisionInfo)
	{
		if ((collisionInfo.gameObject.layer == 7 && GetComponent<InteractObject>().extended && pick) || (collisionInfo.gameObject.layer == 0 && GetComponent<InteractObject>().extended && pick))
		{
			touch = false;
			trigger = true;
			if (hitb)
			{
				SoundManager.Instance.PlaySound(hit);
				hitb = false;
				pickPos = GameObject.Find("Pickaxe00").transform.position;
				pickRot = GameObject.Find("Pickaxe00").transform.rotation;
			}
			_ = player.GetComponent<FirstPersonController>().climbRay;
		}
	}
}
public class Club : MonoBehaviour
{
	private GameObject player;

	public AudioClip hold;

	public AudioClip hit;

	public bool trigger;

	private bool hitb;

	public bool touch;

	private bool wall;

	private float slopForce;

	[SerializeField]
	private float wallImpactForce = 25f;

	[SerializeField]
	private float groundImpactForce = 18f;

	private void Awake()
	{
		player = GameObject.Find("Player");
	}

	private void Start()
	{
		slopForce = player.GetComponent<FirstPersonController>().slopeForce;
	}

	private void Update()
	{
		if (GetComponent<InteractObject>().inHands && !player.GetComponent<FirstPersonController>().pause)
		{
			if (Input.GetButtonDown("Fire1"))
			{
				touch = true;
			}
			if (Input.GetButtonUp("Fire1"))
			{
				touch = false;
			}
			if (player.GetComponent<FirstPersonController>().frontRay && !player.GetComponent<FirstPersonController>().characterController.isGrounded)
			{
				wall = true;
			}
			else if (player.GetComponent<FirstPersonController>().rotationX < 45f && player.GetComponent<FirstPersonController>().characterController.isGrounded)
			{
				wall = true;
			}
			else
			{
				wall = false;
			}
		}
	}

	private void OnTriggerStay(Collider collisionInfo)
	{
		if (touch && collisionInfo.tag != "Player" && GetComponent<InteractObject>().inHands)
		{
			SoundManager.Instance.PlaySound(hit);
			touch = false;
			if (!player.GetComponent<FirstPersonController>().clubRay && player.GetComponent<FirstPersonController>().rotationX > -75f && !player.GetComponent<FirstPersonController>().characterController.isGrounded && !player.GetComponent<FirstPersonController>().rightRay && !player.GetComponent<FirstPersonController>().leftRay && player.GetComponent<FirstPersonController>().rotationX < 45f && !player.GetComponent<FirstPersonController>().downRay)
			{
				player.GetComponent<FirstPersonController>().moveDirection.y = groundImpactForce / 1.5f;
			}
			else if (wall && (player.GetComponent<FirstPersonController>().frontRay || player.GetComponent<FirstPersonController>().clubRay))
			{
				player.GetComponent<FirstPersonController>().AddForce(new Vector3(0f - player.GetComponent<FirstPersonController>().playerCamera.transform.forward.x, 0f, 0f - player.GetComponent<FirstPersonController>().playerCamera.transform.forward.z), wallImpactForce);
			}
			else if (!player.GetComponent<FirstPersonController>().backRay && !player.GetComponent<FirstPersonController>().rightRay && !player.GetComponent<FirstPersonController>().characterController.isGrounded)
			{
				player.GetComponent<FirstPersonController>().AddForce(-player.GetComponent<FirstPersonController>().playerCamera.transform.forward, groundImpactForce);
			}
			else if (player.GetComponent<FirstPersonController>().characterController.isGrounded && player.GetComponent<FirstPersonController>().downRay)
			{
				player.GetComponent<FirstPersonController>().slopeForce = 0f;
				player.GetComponent<FirstPersonController>().moveDirection.y = 0f;
				player.GetComponent<FirstPersonController>().AddForce(-player.GetComponent<FirstPersonController>().playerCamera.transform.forward, groundImpactForce);
			}
			else
			{
				player.GetComponent<FirstPersonController>().AddForce(-player.GetComponent<FirstPersonController>().playerCamera.transform.forward, 0f);
			}
		}
	}

	private void OnTriggerExit()
	{
		player.GetComponent<FirstPersonController>().slopeForce = slopForce;
	}
}
public class EnterZone : MonoBehaviour
{
	private float indoortimer;

	private float outdoortimer;

	[SerializeField]
	private AudioClip hitTrainClip;

	[HideInInspector]
	public bool inLift;

	public AudioClip endGameClip;

	public bool inGirlZone;

	private void Start()
	{
		outdoortimer = 4f;
		indoortimer = 0f;
	}

	private void Update()
	{
		indoortimer -= Time.deltaTime;
		outdoortimer -= Time.deltaTime;
		if (indoortimer > 0f)
		{
			SoundManager.Instance.IndoorBlend();
		}
		if (outdoortimer > 0f)
		{
			SoundManager.Instance.OutdoorBlend();
		}
	}

	private void OnTriggerEnter(Collider collisionInfo)
	{
		if (collisionInfo.tag == "Door/Indoor")
		{
			indoortimer = 4f;
			outdoortimer = 0f;
		}
		if (collisionInfo.tag == "Door/Outdoor")
		{
			outdoortimer = 4f;
			indoortimer = 0f;
		}
		if (collisionInfo.tag == "Subway/In")
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().inSubway = true;
		}
		if (collisionInfo.tag == "Subway/Out")
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().inSubway = false;
		}
		if (collisionInfo.tag == "Club/In")
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().inClub = true;
		}
		if (collisionInfo.tag == "Club/Out")
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().inClub = false;
		}
		if (collisionInfo.tag == "Achievement/wayClimber")
		{
			if (!GameObject.Find("GameManager").GetComponent<GameManager>().wayClimber)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().Popup();
			}
			GameObject.Find("GameManager").GetComponent<GameManager>().wayClimber = true;
			GameObject.Find("GameManager").GetComponent<GameManager>().wayClimberState = 1;
			GameObject.Find("GameManager").GetComponent<GameManager>().lastAchievement = "Way of the climber";
			SteamIntegration.Instance.UnlockAchivement("WayOfTheClimber");
		}
		if (collisionInfo.tag == "Train")
		{
			if (GameObject.Find("GameManager").GetComponent<GameManager>().gameTime < 240f && !GameObject.Find("GameManager").GetComponent<GameManager>().gameUnder)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().Popup();
				GameObject.Find("GameManager").GetComponent<GameManager>().gameUnder = true;
				GameObject.Find("GameManager").GetComponent<GameManager>().gameUnderState = 1;
				GameObject.Find("GameManager").GetComponent<GameManager>().lastAchievement = "Way of the rusher";
				UnityEngine.Debug.Log("warusher");
				SteamIntegration.Instance.UnlockAchivement("WayOfTheRusher");
			}
			SoundManager.Instance.PlayMusicShot(endGameClip);
			GameObject.Find("GameManager").GetComponent<GameManager>().endGame = true;
		}
		if (collisionInfo.tag == "KillZ")
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().ReturnHome();
		}
		if (collisionInfo.tag == "MusicZone" && collisionInfo.GetComponent<MusicZone>().timer < 0f)
		{
			SoundManager.Instance.PlayMusicShot(collisionInfo.GetComponent<MusicZone>().music);
			collisionInfo.GetComponent<MusicZone>().timer = collisionInfo.GetComponent<MusicZone>().countown;
		}
		if (collisionInfo.tag == "MusicZonePartTwo" && GameObject.Find("GameManager").GetComponent<GameManager>().secondPart == 1 && collisionInfo.GetComponent<MusicZone>().timer < 0f)
		{
			SoundManager.Instance.PlayMusicShot(collisionInfo.GetComponent<MusicZone>().music);
			collisionInfo.GetComponent<MusicZone>().timer = collisionInfo.GetComponent<MusicZone>().countown;
		}
		if (collisionInfo.tag == "MusicZonePartOne" && GameObject.Find("GameManager").GetComponent<GameManager>().secondPart == 0 && collisionInfo.GetComponent<MusicZone>().timer < 0f)
		{
			SoundManager.Instance.PlayMusicShot(collisionInfo.GetComponent<MusicZone>().music);
			collisionInfo.GetComponent<MusicZone>().timer = collisionInfo.GetComponent<MusicZone>().countown;
		}
		if (collisionInfo.tag == "SavingZone")
		{
			GameObject.Find("GameManager").GetComponent<SaveLoadSystem>().Save();
			GameObject.Find("GameManager").GetComponent<GameManager>().savePopup = true;
		}
		if (collisionInfo.tag == "TrainDeath" && !GameObject.Find("GameManager").GetComponent<GameManager>().endGame)
		{
			if (!GameObject.Find("GameManager").GetComponent<GameManager>().trainDeath)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().Popup();
			}
			GameObject.Find("GameManager").GetComponent<GameManager>().trainDeath = true;
			GameObject.Find("GameManager").GetComponent<GameManager>().trainDeathState = 1;
			GameObject.Find("GameManager").GetComponent<GameManager>().lastAchievement = "Flat face";
			SteamIntegration.Instance.UnlockAchivement("FlatFace");
			base.transform.position = GameObject.Find("SpawnPoint").transform.position;
		}
		if (collisionInfo.tag == "TrainDeath" && !GameObject.Find("GameManager").GetComponent<GameManager>().endGame)
		{
			SoundManager.Instance.PlaySound(hitTrainClip);
			GameObject.Find("DeathAnim").GetComponent<DeathAnim>().Trigger();
			base.transform.position = GameObject.Find("SpawnPoint").transform.position;
		}
		if (collisionInfo.tag == "GirlZone")
		{
			inGirlZone = true;
		}
	}

	private void OnTriggerStay(Collider collisionInfo)
	{
		if (collisionInfo.tag == "Lift")
		{
			base.transform.SetParent(collisionInfo.GetComponent<SetLift>().lift.transform);
			inLift = true;
		}
		if (collisionInfo.tag == "Train")
		{
			base.transform.parent = collisionInfo.transform;
		}
		if (collisionInfo.tag == "GirlZone")
		{
			inGirlZone = true;
		}
	}

	private void OnTriggerExit(Collider collisionInfo)
	{
		base.transform.parent = null;
		inLift = false;
		inGirlZone = false;
	}
}
public class FirstPersonController : MonoBehaviour
{
	public bool InAirCrouch;

	public bool preslide;

	[Header("Functional Options")]
	[SerializeField]
	private bool canSprint = true;

	[SerializeField]
	private bool canJump = true;

	[SerializeField]
	private bool canCrouch = true;

	[SerializeField]
	private bool canUseHeadbob = true;

	[SerializeField]
	private bool WillSlideOnSlopes = true;

	[SerializeField]
	private bool useFootsteps = true;

	[SerializeField]
	private bool canInteract = true;

	[Header("Fall")]
	[SerializeField]
	private AudioClip bigImpact;

	[SerializeField]
	private AudioClip smallImpact;

	[SerializeField]
	private AudioClip stoneFall;

	[SerializeField]
	private AudioClip concreteFall;

	[SerializeField]
	private AudioClip metalFall;

	[SerializeField]
	private AudioClip tauleFall;

	[SerializeField]
	private AudioClip grassFall;

	[SerializeField]
	private AudioClip dirtFall;

	[SerializeField]
	private AudioClip sandFall;

	[SerializeField]
	private AudioClip waterFall;

	[SerializeField]
	private AudioClip woodFall;

	[SerializeField]
	private AudioSource fallAudio;

	[SerializeField]
	private GameObject screenshake;

	private float fallTime;

	[SerializeField]
	private float maxFallTime = 2f;

	[Header("Controls")]
	[SerializeField]
	private KeyCode sprintKey = KeyCode.LeftShift;

	[SerializeField]
	private KeyCode jumpKey = KeyCode.Space;

	[SerializeField]
	private KeyCode crouchKey = KeyCode.LeftControl;

	[SerializeField]
	private KeyCode interactKey = KeyCode.E;

	[SerializeField]
	private KeyCode sprintKey1 = KeyCode.LeftShift;

	[SerializeField]
	private KeyCode jumpKey1 = KeyCode.Space;

	[SerializeField]
	private KeyCode crouchKey1 = KeyCode.LeftControl;

	[SerializeField]
	private KeyCode interactKey1 = KeyCode.E;

	[Header("Movement Parameters")]
	[SerializeField]
	private float walkSpeed = 3f;

	[SerializeField]
	private float sprintSpeed = 6f;

	[SerializeField]
	private float crouchSpeed = 1.5f;

	[SerializeField]
	private float inairCrouchSpeed = 1f;

	[SerializeField]
	private float slopeSlideSpeed = 7f;

	[SerializeField]
	private float slopeSpeed = 8f;

	[SerializeField]
	private float sprintSlideImpulsion = 5f;

	[SerializeField]
	private float slideImpulsion = 5f;

	[SerializeField]
	private float boostPower = 8f;

	[Header("Look Parameters")]
	[SerializeField]
	[Range(1f, 10f)]
	public float lookSpeedX = 2f;

	[SerializeField]
	[Range(1f, 10f)]
	public float lookSpeedY = 2f;

	[SerializeField]
	private float upperLookLimit = 90f;

	[SerializeField]
	private float lowerLookLimit = 90f;

	[SerializeField]
	private Vector3 offset;

	private Quaternion initialRotation;

	[Header("Jumping Parameters")]
	[SerializeField]
	public float jumpForce = 8f;

	[SerializeField]
	private float gravity = 30f;

	public AudioClip jumpClip;

	public AudioClip landClip;

	public bool landing;

	public float landTimer;

	public float airTime;

	private float aftervaultjumpTimer;

	[Header("Crouch Parameters")]
	[SerializeField]
	private float crouchingSpeed = 0.3f;

	[SerializeField]
	private float standHeight = 2f;

	[SerializeField]
	private float crouchHeight = 1f;

	public bool isCrouching;

	private float crouchTimer;

	private float headbobEndTimer;

	public bool standing;

	[Header("Headbob Parameters")]
	[SerializeField]
	public float walkBobSpeed = 14f;

	[SerializeField]
	public float walkBobAmount = 0.05f;

	[SerializeField]
	public float sprintBobSpeed = 18f;

	[SerializeField]
	public float sprintBobAmount = 0.11f;

	[SerializeField]
	public float crouchBobSpeed = 8f;

	[SerializeField]
	public float crouchBobAmount = 0.025f;

	[SerializeField]
	public float fallBobSpeed = 8f;

	[SerializeField]
	public float fallBobUpSpeed = 2f;

	[SerializeField]
	public float fallBobAmount = 0.025f;

	private float defaultYPos;

	public float timer;

	public float fallTimer;

	[Header("Camera Controller")]
	[SerializeField]
	private float zoomSpeed = 5f;

	[SerializeField]
	private float zoomFOV = 30f;

	[SerializeField]
	private float runFOV = 90f;

	[SerializeField]
	private float tiltAmount = 7f;

	[SerializeField]
	private float tiltSpeed = 7f;

	[SerializeField]
	private float compassLookSpeed = 12f;

	[SerializeField]
	private float smoothCamSpeed = 2f;

	[SerializeField]
	private GameObject targetObject;

	public bool invertX;

	public bool invertY;

	private int invertXFactor;

	private int invertYFactor;

	public bool compassLook;

	public bool secretLook;

	private bool isZooming;

	private float defaultFOV;

	public bool camController = true;

	public bool smoothCam;

	[Header("Footsteps Parameters")]
	[SerializeField]
	private float baseStepSpeed = 0.5f;

	[SerializeField]
	private float crouchStepMultiplier = 1.5f;

	[SerializeField]
	private float sprintStepMultiplier = 0.6f;

	[SerializeField]
	private AudioSource playerSource;

	[SerializeField]
	private AudioClip[] stoneClips;

	[SerializeField]
	private AudioClip[] concreteClips;

	[SerializeField]
	private AudioClip[] metalClips;

	[SerializeField]
	private AudioClip[] tauleClips;

	[SerializeField]
	private AudioClip[] grassClips;

	[SerializeField]
	private AudioClip[] dirtClips;

	[SerializeField]
	private AudioClip[] sandClips;

	[SerializeField]
	private AudioClip[] waterClips;

	[SerializeField]
	private AudioClip[] woodClips;

	private float footstepTimer;

	private Vector3 hitPointNormal;

	private bool onFlatGround;

	[Header("Slope Parameters")]
	[SerializeField]
	public float slopeForce;

	[SerializeField]
	private float slopeForceRayLength;

	private bool prejump;

	private float prejumpCancelTimer;

	public bool inJump;

	[Header("Interaction")]
	[SerializeField]
	private Vector3 interactionRayPoint;

	[SerializeField]
	private float interactionDistance;

	[SerializeField]
	private float maxInteractionDistance;

	[SerializeField]
	private float sphereRadius;

	[SerializeField]
	private float maxSphereDistance;

	[SerializeField]
	private float currentHitDistance;

	[SerializeField]
	private LayerMask interactionLayer;

	[SerializeField]
	private LayerMask allinteractLayer;

	[SerializeField]
	private LayerMask nomotoLayer;

	[SerializeField]
	private LayerMask doorLayer;

	[SerializeField]
	private LayerMask inHandsLayer;

	[SerializeField]
	private LayerMask npcLayer;

	[SerializeField]
	private LayerMask motoLayer;

	[SerializeField]
	private LayerMask defaultLayer;

	public Interactable currentInteractable;

	public GameObject currentObject;

	public GameObject dialogueBox;

	public bool dialogueActive;

	[Header("Objects")]
	public GameObject club;

	public GameObject climber;

	public GameObject propeller;

	public GameObject blower;

	public GameObject grabber;

	public GameObject motorBike;

	public bool bigball;

	[SerializeField]
	private float ballWeight = 0.3f;

	private float currentWeight = 1f;

	public bool inHands;

	public bool canThrow;

	public bool frontRay;

	public bool shortfrontRay;

	public bool vaulRayDown;

	public bool vaulRayUp;

	public bool rightRay;

	public bool leftRay;

	public bool throwRay;

	public bool backRay;

	public bool downRay;

	public bool headRay;

	public bool interactionRay;

	public bool interactionSphere;

	public bool upRay;

	public bool crouchUpRay;

	public bool clubRay;

	public bool climbRay;

	[Header("System")]
	public bool pause;

	public GameObject pauseMenu;

	public GameObject firstInterface;

	public GameObject library;

	public GameObject map;

	public GameObject options;

	public GameObject confirmExit;

	public GameObject confirmReset;

	public GameObject confirmReturn;

	public GameObject controls;

	public GameObject talkPopup;

	public GameObject grabPopup;

	public GameObject noticketPopup;

	public GameObject jukeboxPopup;

	public GameObject elevatorPopup;

	public GameObject doorPopup;

	public Camera playerCamera;

	[HideInInspector]
	public CharacterController characterController;

	public Vector3 moveDirection;

	private Vector2 currentInput;

	public Vector2 currentInputRaw;

	public float rotationX;

	public float rotationZ;

	public static FirstPersonController instance;

	[SerializeField]
	private GameObject startButton;

	[SerializeField]
	private GameObject selectButton;

	public float jumpTimer;

	public bool vault;

	public bool addingForce;

	public Vector3 forceAdded;

	public Vector3 force;

	public float forceFactor;

	public float initforceFactor;

	public float forceSpeed = 4f;

	public float tempforceSpeed;

	[Range(0f, 100f)]
	public float deceleration = 1f;

	private float verticalInput;

	private float horizontalInput;

	private float verticalInputRaw;

	private float horizontalInputRaw;

	[Header("Camera mouse incidence")]
	public float rotationAmount = 4f;

	public float maxRotationAmount = 5f;

	public float smoothRotation = 12f;

	public float compassTimer;

	public float secretTimer;

	public Quaternion initRotationX;

	public float initRotationY;

	public float slideTimer;

	private float tempslideCountdown;

	public float slideCountdown = 1f;

	public float keyUpTimer;

	public GameObject currentHitObject;

	public bool jumpSlope;

	private bool leaveSlope;

	public bool canMove { get; private set; } = true;


	public bool IsSprinting
	{
		get
		{
			if (canSprint && (Input.GetKey(sprintKey) || Input.GetAxis("Sprint") > 0.4f) && currentInputRaw != new Vector2(0f, 0f))
			{
				return !motorBike.GetComponent<MotorBike>().isActive;
			}
			return false;
		}
	}

	private bool ShouldJump
	{
		get
		{
			if (Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump"))
			{
				return characterController.isGrounded;
			}
			return false;
		}
	}

	public bool ShouldCrouch
	{
		get
		{
			if ((Input.GetKey(crouchKey) || Input.GetKey(KeyCode.C) || Input.GetButton("Slide") || Input.GetKey(KeyCode.LeftAlt)) && characterController.isGrounded)
			{
				return !motorBike.GetComponent<MotorBike>().isActive;
			}
			return false;
		}
	}

	private bool ShouldCrouchInAir
	{
		get
		{
			if ((Input.GetKey(crouchKey) || Input.GetKey(KeyCode.C) || Input.GetButton("Slide") || Input.GetKey(KeyCode.LeftAlt)) && !characterController.isGrounded)
			{
				return !OnSlope();
			}
			return false;
		}
	}

	private bool FlatSlide
	{
		get
		{
			if ((Input.GetKeyDown(crouchKey) || Input.GetKeyDown(KeyCode.C) || Input.GetButtonDown("Slide") || Input.GetKeyDown(KeyCode.LeftAlt)) && characterController.isGrounded)
			{
				return currentInputRaw != Vector2.zero;
			}
			return false;
		}
	}

	private float GetCurrentOffset
	{
		get
		{
			if (!isCrouching)
			{
				if (!IsSprinting)
				{
					return baseStepSpeed;
				}
				return baseStepSpeed * sprintStepMultiplier;
			}
			return baseStepSpeed * crouchStepMultiplier;
		}
	}

	private bool IsSliding
	{
		get
		{
			if (characterController.isGrounded && Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, 3f))
			{
				hitPointNormal = hitInfo.normal;
				return Vector3.Angle(hitPointNormal, Vector3.up) > characterController.slopeLimit;
			}
			return false;
		}
	}

	private bool CrouchSliding
	{
		get
		{
			if (characterController.isGrounded && Physics.Raycast(base.transform.position, Vector3.down, out var _, characterController.height / 2f * slopeForceRayLength) && ShouldCrouch)
			{
				return true;
			}
			return false;
		}
	}

	private bool OnSlope()
	{
		if (!characterController.isGrounded)
		{
			return false;
		}
		if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, slopeForceRayLength) && hitInfo.normal != Vector3.up && Vector3.Angle(hitInfo.normal, Vector3.up) > 4f)
		{
			return true;
		}
		return false;
	}

	private bool OnCeilingSlope()
	{
		if (characterController.isGrounded)
		{
			return false;
		}
		if (Physics.Raycast(playerCamera.transform.position, Vector3.up, out var hitInfo, 1f) && hitInfo.normal != Vector3.up && Vector3.Angle(hitInfo.normal, Vector3.up) > 10f)
		{
			return true;
		}
		return false;
	}

	private bool OnSlopeAir()
	{
		if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, slopeForceRayLength) && hitInfo.normal != Vector3.up && Vector3.Angle(hitInfo.normal, Vector3.up) < 65f)
		{
			return true;
		}
		return false;
	}

	private bool OnSteepSlope()
	{
		if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, slopeForceRayLength) && hitInfo.normal != Vector3.up && GetComponent<Slope>().uphill && Vector3.Angle(hitInfo.normal, Vector3.up) > 40f)
		{
			return true;
		}
		return false;
	}

	private void Awake()
	{
		instance = this;
		playerCamera = GetComponentInChildren<Camera>();
		characterController = GetComponent<CharacterController>();
		defaultYPos = characterController.center.y + characterController.height / 2f + offset.y;
		defaultFOV = playerCamera.fieldOfView;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
		club = GameObject.Find("Club");
		climber = GameObject.Find("Climber");
		propeller = GameObject.Find("Propeller");
		blower = GameObject.Find("Blower");
		grabber = GameObject.Find("Grabber");
		motorBike = GameObject.Find("Moto");
		initialRotation = playerCamera.transform.rotation;
	}

	private void Start()
	{
		if (GameObject.Find("SavingOptions").GetComponent<SavingOptions>().mouseInvertedX == 1)
		{
			invertX = true;
		}
		else
		{
			invertX = false;
		}
		if (GameObject.Find("SavingOptions").GetComponent<SavingOptions>().mouseInvertedY == 1)
		{
			invertY = true;
		}
		else
		{
			invertY = false;
		}
	}

	private void Update()
	{
		if (dialogueActive)
		{
			talkPopup.SetActive(value: false);
		}
		if (IsSliding)
		{
			onFlatGround = false;
		}
		else
		{
			onFlatGround = true;
		}
		if (pause || GameObject.Find("Outro").GetComponent<Outro>().timer < -4f)
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
		else
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
		if ((Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Start")) && !GameObject.Find("GameManager").GetComponent<GameManager>().endGame)
		{
			pauseMenu.SetActive(!pauseMenu.activeSelf);
			firstInterface.SetActive(value: true);
			library.SetActive(value: false);
			options.SetActive(value: false);
			confirmExit.SetActive(value: false);
			confirmReset.SetActive(value: false);
			confirmReturn.SetActive(value: false);
			controls.SetActive(value: false);
			map.SetActive(value: false);
			if (GameObject.Find("Canvas") != null)
			{
				GameObject.Find("Canvas").GetComponent<MenuInput>().OnPress();
			}
			if (pause)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			if (GameObject.Find("GameManager").GetComponent<GameManager>().gamepad)
			{
				EventSystem current = EventSystem.current;
				current.SetSelectedGameObject(startButton, new BaseEventData(current));
			}
		}
		if ((Input.GetKeyDown(KeyCode.Tab) || Input.GetButtonDown("Select")) && !GameObject.Find("GameManager").GetComponent<GameManager>().endGame)
		{
			pauseMenu.SetActive(!pauseMenu.activeSelf);
			firstInterface.SetActive(value: false);
			options.SetActive(value: false);
			library.SetActive(value: true);
			confirmExit.SetActive(value: false);
			confirmReset.SetActive(value: false);
			confirmReturn.SetActive(value: false);
			controls.SetActive(value: false);
			map.SetActive(value: false);
			if (pause)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			if (GameObject.Find("GameManager").GetComponent<GameManager>().gamepad)
			{
				EventSystem current2 = EventSystem.current;
				current2.SetSelectedGameObject(selectButton, new BaseEventData(current2));
			}
		}
		if (Input.GetKeyDown(KeyCode.M) && !GameObject.Find("GameManager").GetComponent<GameManager>().endGame && GameObject.Find("GameManager").GetComponent<GameManager>().haveMap == 1)
		{
			pauseMenu.SetActive(!pauseMenu.activeSelf);
			map.SetActive(value: true);
			firstInterface.SetActive(value: false);
			options.SetActive(value: false);
			library.SetActive(value: false);
			confirmExit.SetActive(value: false);
			confirmReset.SetActive(value: false);
			confirmReturn.SetActive(value: false);
			controls.SetActive(value: false);
			if (pause)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
		}
		if (pauseMenu.activeSelf)
		{
			pause = true;
		}
		else
		{
			pause = false;
		}
		if (Physics.Raycast(base.transform.position, Vector3.down, out var _, 1.3f) && (Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump")) && characterController.velocity.y < 0f)
		{
			prejump = true;
			prejumpCancelTimer = 0.4f;
		}
		frontRay = Physics.Raycast(playerCamera.transform.position, base.transform.forward, out var _, 4f);
		shortfrontRay = Physics.Raycast(playerCamera.transform.position, base.transform.forward, out var _, 1f);
		vaulRayDown = Physics.Raycast(base.transform.position, base.transform.forward, out var _, 1.3f, defaultLayer);
		vaulRayUp = Physics.Raycast(base.transform.position + Vector3.up, base.transform.forward, out var _, 1.3f, defaultLayer);
		leftRay = Physics.Raycast(base.transform.position + Vector3.up, -base.transform.right, out var _, 2f);
		rightRay = Physics.Raycast(base.transform.position + Vector3.up, base.transform.right, out var _, 2f);
		throwRay = Physics.Raycast(playerCamera.transform.position, base.transform.forward, out var _, 1f);
		backRay = Physics.Raycast(base.transform.position + Vector3.up, -base.transform.forward, out var _, 0.9f);
		headRay = Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var _, 1f);
		interactionRay = Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var _, interactionDistance);
		upRay = Physics.Raycast(playerCamera.transform.position, base.transform.up, out var _, 1f);
		downRay = Physics.Raycast(base.transform.position, -base.transform.up, out var _, 1.2f);
		crouchUpRay = Physics.Raycast(base.transform.position + base.transform.up / 5f, base.transform.up, out var _, 1.99f);
		clubRay = Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var _, 4f);
		climbRay = Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var _, 3f);
		dialogueActive = dialogueBox.activeSelf;
		if (!characterController.isGrounded)
		{
			landing = true;
			airTime -= Time.deltaTime;
			if (characterController.velocity.y > 0f)
			{
				fallTime = Mathf.Lerp(fallTime, 0f, 6f * Time.deltaTime);
			}
			else if (characterController.velocity.y < 0f && airTime < -0.6f && !pause)
			{
				fallTime += Time.deltaTime;
			}
		}
		else
		{
			InAirCrouch = false;
			airTime = 0.3f;
			fallTime = Mathf.Lerp(fallTime, 0f, 6f * Time.deltaTime);
		}
		fallAudio.volume = Mathf.Lerp(0f, 1f, fallTime / maxFallTime);
		landTimer -= Time.deltaTime;
		headbobEndTimer -= Time.deltaTime;
		aftervaultjumpTimer -= Time.deltaTime;
		prejumpCancelTimer -= Time.deltaTime;
		if (GameObject.Find("GameManager").GetComponent<GameManager>().endGame)
		{
			canMove = false;
		}
		else if (climber != null)
		{
			if (pauseMenu.activeSelf)
			{
				canMove = false;
			}
			else if (climber.GetComponent<Climber>().trigger)
			{
				canMove = false;
			}
			else if (GetComponent<EnterZone>().inLift && GameObject.Find("GameManager").GetComponent<GameManager>().inActivatedLift)
			{
				canMove = false;
			}
			else if (GameObject.Find("GameManager").GetComponent<GameManager>().bridgeTimer > 0f)
			{
				canMove = false;
			}
			else
			{
				canMove = !dialogueActive;
			}
		}
		if (climber == null)
		{
			canMove = !dialogueActive;
		}
		if (canInteract)
		{
			HandleInteractionCheck();
			HandleInteractionInput();
		}
		if (!pause && GameObject.Find("Outro").GetComponent<Outro>().timer > -4f)
		{
			HandleCameraController();
			HandleMouseLook();
		}
		CalculateMovementInput();
		HandleAddingForce();
		if (canMove)
		{
			HandleMovementInput();
			if (characterController.isGrounded && !motorBike.GetComponent<InteractObject>().onMoto)
			{
				MovementBoost();
			}
			if (canJump)
			{
				HandleJump();
			}
			if (canCrouch)
			{
				HandleCrouch();
			}
			if (canUseHeadbob && standing && !motorBike.GetComponent<InteractObject>().onMoto)
			{
				HandleHeadbob();
			}
			if (useFootsteps && !motorBike.GetComponent<InteractObject>().onMoto)
			{
				HandleFootsteps();
			}
			CheckForVault();
			ApplyFinalMovements();
			if (characterController.isGrounded && !GetComponent<EnterZone>().inLift)
			{
				moveDirection.y = -0.5f;
				if (landing && airTime < 0f)
				{
					landTimer = 0.2f;
					landing = false;
					RaycastHit hitInfo17;
					if (fallTime > 1.5f)
					{
						SoundManager.Instance.PlaySound(bigImpact);
						screenshake.GetComponent<Screenshake>().duration = screenshake.GetComponent<Screenshake>().bigFallDuration;
						screenshake.GetComponent<Screenshake>().strengthCurve = screenshake.GetComponent<Screenshake>().bigCurve;
						screenshake.GetComponent<Screenshake>().start = true;
					}
					else if (fallTime > 0.8f)
					{
						SoundManager.Instance.PlaySound(smallImpact);
						screenshake.GetComponent<Screenshake>().duration = screenshake.GetComponent<Screenshake>().smallFallDuration;
						screenshake.GetComponent<Screenshake>().strengthCurve = screenshake.GetComponent<Screenshake>().smallCurve;
						screenshake.GetComponent<Screenshake>().start = true;
					}
					else if (Physics.Raycast(base.transform.position, Vector3.down, out hitInfo17, 3f))
					{
						switch (hitInfo17.collider.tag)
						{
						case "Footsteps/Metal":
							SoundManager.Instance.PlaySound(metalFall);
							break;
						case "Footsteps/Taule":
							SoundManager.Instance.PlaySound(tauleClips[UnityEngine.Random.Range(0, tauleClips.Length - 1)]);
							break;
						case "Footsteps/Wood":
							SoundManager.Instance.PlaySound(woodFall);
							break;
						case "Footsteps/Concrete":
							SoundManager.Instance.PlaySound(concreteFall);
							break;
						case "Footsteps/Stone":
							SoundManager.Instance.PlaySound(stoneFall);
							break;
						case "Footsteps/Grass":
							SoundManager.Instance.PlaySound(grassFall);
							break;
						case "Footsteps/Dirt":
							SoundManager.Instance.PlaySound(dirtFall);
							break;
						case "Footsteps/Sand":
							SoundManager.Instance.PlaySound(sandFall);
							break;
						case "Footsteps/Water":
							SoundManager.Instance.PlaySound(waterFall);
							break;
						default:
							SoundManager.Instance.PlaySound(stoneFall);
							break;
						}
					}
					if (slideTimer > 0f && !Input.GetKey(crouchKey) && !Input.GetKey(KeyCode.C) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetButton("Slide"))
					{
						forceFactor = 0f;
					}
				}
			}
			UnityEngine.Debug.DrawRay(base.transform.position + base.transform.up, base.transform.up * 1.99f, UnityEngine.Color.green);
			if (characterController.isGrounded)
			{
				inJump = false;
			}
			if (bigball)
			{
				currentWeight = ballWeight;
			}
			else
			{
				currentWeight = 1f;
			}
		}
		jumpTimer -= Time.deltaTime;
		if ((Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump")) && characterController.isGrounded)
		{
			jumpTimer = 0.3f;
		}
	}

	private void MovementBoost()
	{
	}

	private void CheckForVault()
	{
		if (!shortfrontRay && !characterController.isGrounded && !downRay && vaulRayDown && !vaulRayUp && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetAxis("Vertical") > 0.15f))
		{
			vault = true;
			moveDirection.y = 5f;
			aftervaultjumpTimer = 0.5f;
		}
		else if (aftervaultjumpTimer > 0f && downRay && !characterController.isGrounded)
		{
			moveDirection.y = -1.5f;
			vault = false;
		}
		else
		{
			vault = false;
		}
	}

	public void AddForce(Vector3 tempforce, float tempforceFactor)
	{
		deceleration = 1f;
		moveDirection = new Vector3(0f, 0f, 0f);
		forceFactor = tempforceFactor;
		force = tempforce;
		moveDirection.y = force.y * forceFactor;
		tempforceSpeed = forceSpeed;
		initforceFactor = tempforceFactor;
	}

	public void AddAccumulatedForce(Vector3 tempforce, float tempforceFactor)
	{
		moveDirection = new Vector3(moveDirection.x, moveDirection.y, moveDirection.z);
		forceFactor = tempforceFactor;
		force = tempforce;
		moveDirection.y = force.y * forceFactor;
		tempforceSpeed = forceSpeed;
		initforceFactor = tempforceFactor;
	}

	public void AddHorizontalForce(Vector3 tempforce, float tempforceFactor)
	{
		deceleration = 1f;
		moveDirection = new Vector3(0f, moveDirection.y, 0f);
		forceFactor = tempforceFactor;
		force = tempforce;
		tempforceSpeed = forceSpeed;
		initforceFactor = tempforceFactor;
	}

	public void AddVerticalForce(Vector3 tempforce, float tempforceFactor)
	{
		deceleration = 1f;
		moveDirection = new Vector3(moveDirection.x, -1f, moveDirection.z);
		forceFactor = tempforceFactor;
		force = tempforce;
		moveDirection.y = force.y * forceFactor;
		tempforceSpeed = forceSpeed;
		initforceFactor = tempforceFactor;
	}

	private void HandleAddingForce()
	{
		if (initforceFactor > 20f && tempforceSpeed < 10f)
		{
			tempforceSpeed = 10f;
		}
		else if (tempforceSpeed < 2f)
		{
			tempforceSpeed = 2f;
		}
		if (!characterController.isGrounded)
		{
			if (forceFactor < 0f)
			{
				forceFactor = 0f;
			}
			if (forceFactor > 0f)
			{
				tempforceSpeed -= Time.deltaTime * initforceFactor / 2f;
			}
		}
		else
		{
			tempforceSpeed = forceSpeed;
		}
		forceFactor -= Time.deltaTime * (tempforceSpeed * deceleration);
		if (forceFactor > 0f)
		{
			addingForce = true;
			forceAdded = force * forceFactor;
		}
		else
		{
			addingForce = false;
			forceAdded = new Vector3(0f, 0f, 0f);
			forceFactor = 0f;
			force = new Vector3(0f, 0f, 0f);
		}
		if (forceFactor <= 1f)
		{
			deceleration = 1f;
		}
	}

	private void CalculateMovementInput()
	{
		if (Input.GetKey(KeyCode.D) && (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q)))
		{
			horizontalInput = 0f;
		}
		else if (Input.GetKey(KeyCode.D))
		{
			horizontalInput = Mathf.Lerp(horizontalInput, 1f, 15f * Time.deltaTime);
		}
		else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q))
		{
			horizontalInput = Mathf.Lerp(horizontalInput, -1f, 15f * Time.deltaTime);
		}
		else if (Input.GetAxis("horizontalArrows") != 0f)
		{
			horizontalInput = Input.GetAxis("horizontalArrows");
		}
		else if (Input.GetAxis("Horizontal") != 0f)
		{
			horizontalInput = Input.GetAxis("Horizontal");
		}
		else
		{
			horizontalInput = Mathf.Lerp(horizontalInput, 0f, 15f * Time.deltaTime);
		}
		if ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z)) && Input.GetKey(KeyCode.S))
		{
			verticalInput = (verticalInput = 0f);
		}
		else if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z))
		{
			verticalInput = Mathf.Lerp(verticalInput, 1f, 15f * Time.deltaTime);
		}
		else if (Input.GetKey(KeyCode.S))
		{
			verticalInput = Mathf.Lerp(verticalInput, -1f, 15f * Time.deltaTime);
		}
		else if (Input.GetAxis("verticalArrows") != 0f)
		{
			verticalInput = Input.GetAxis("verticalArrows");
		}
		else if (Input.GetAxis("Vertical") != 0f)
		{
			verticalInput = Input.GetAxis("Vertical");
		}
		else
		{
			verticalInput = Mathf.Lerp(verticalInput, 0f, 10f * Time.deltaTime);
		}
		if (Input.GetKey(KeyCode.D) && (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q)))
		{
			horizontalInputRaw = 0f;
		}
		else if (Input.GetKey(KeyCode.D))
		{
			horizontalInputRaw = 1f;
		}
		else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q))
		{
			horizontalInputRaw = -1f;
		}
		else if (Input.GetAxisRaw("horizontalArrows") != 0f)
		{
			horizontalInputRaw = Input.GetAxisRaw("horizontalArrows");
		}
		else if (Input.GetAxisRaw("Horizontal") != 0f)
		{
			horizontalInputRaw = Input.GetAxisRaw("Horizontal");
		}
		else
		{
			horizontalInputRaw = 0f;
		}
		if ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z)) && Input.GetKey(KeyCode.S))
		{
			verticalInputRaw = 0f;
		}
		else if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z))
		{
			verticalInputRaw = 1f;
		}
		else if (Input.GetKey(KeyCode.S))
		{
			verticalInputRaw = -1f;
		}
		else if (Input.GetAxisRaw("verticalArrows") != 0f)
		{
			verticalInputRaw = Input.GetAxisRaw("verticalArrows");
		}
		else if (Input.GetAxisRaw("Vertical") != 0f)
		{
			verticalInputRaw = Input.GetAxisRaw("Vertical");
		}
		else
		{
			verticalInputRaw = 0f;
		}
		if (motorBike.GetComponent<InteractObject>().onMoto)
		{
			currentInput = Vector2.zero;
			currentInputRaw = Vector2.zero;
		}
		else
		{
			currentInput = new Vector2(((InAirCrouch && inJump) ? inairCrouchSpeed : ((isCrouching && force == Vector3.zero) ? crouchSpeed : ((IsSprinting && (slideTimer < 0f || (!Input.GetKey(crouchKey) && !Input.GetKey(KeyCode.C) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetButton("Slide")))) ? sprintSpeed : walkSpeed))) * verticalInput, (InAirCrouch ? inairCrouchSpeed : ((isCrouching && force == Vector3.zero) ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed))) * horizontalInput);
			currentInputRaw = new Vector2(((InAirCrouch && inJump) ? inairCrouchSpeed : ((isCrouching && force == Vector3.zero) ? crouchSpeed : ((IsSprinting && (slideTimer < 0f || (!Input.GetKey(crouchKey) && !Input.GetKey(KeyCode.C) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetButton("Slide")))) ? sprintSpeed : walkSpeed))) * verticalInputRaw, (InAirCrouch ? inairCrouchSpeed : ((isCrouching && force == Vector3.zero) ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed))) * horizontalInputRaw);
		}
	}

	private void HandleMovementInput()
	{
		float y = moveDirection.y;
		if (isCrouching && forceAdded != Vector3.zero && characterController.isGrounded)
		{
			moveDirection = forceAdded;
		}
		else if (club != null && inHands)
		{
			if (blower.GetComponent<Blower>().isActive)
			{
				moveDirection = base.transform.TransformDirection(Vector3.forward) * currentInput.x + base.transform.TransformDirection(Vector3.right) * currentInput.y + blower.GetComponent<Blower>().blowMovement + forceAdded + motorBike.GetComponent<MotorBike>().motorMovement;
			}
			else if (!propeller.GetComponent<Propeller>().isActive)
			{
				moveDirection = base.transform.TransformDirection(Vector3.forward) * currentInput.x + base.transform.TransformDirection(Vector3.right) * currentInput.y + forceAdded + forceAdded + propeller.GetComponent<Propeller>().propellerMovement + motorBike.GetComponent<MotorBike>().motorMovement;
			}
			else
			{
				moveDirection = base.transform.TransformDirection(Vector3.forward) * currentInput.x + base.transform.TransformDirection(Vector3.right) * currentInput.y + forceAdded + propeller.GetComponent<Propeller>().propellerMovement + motorBike.GetComponent<MotorBike>().motorMovement;
			}
		}
		else
		{
			moveDirection = base.transform.TransformDirection(Vector3.forward) * currentInput.x + base.transform.TransformDirection(Vector3.right) * currentInput.y + forceAdded;
		}
		moveDirection.y = y;
	}

	private void HandleMouseLook()
	{
		compassTimer -= Time.deltaTime;
		secretTimer -= Time.deltaTime;
		if (invertX)
		{
			invertXFactor = -1;
		}
		else
		{
			invertXFactor = 1;
		}
		if (invertY)
		{
			invertYFactor = -1;
		}
		else
		{
			invertYFactor = 1;
		}
		Quaternion quaternion = Quaternion.LookRotation(targetObject.transform.position - playerCamera.transform.position);
		Quaternion quaternion2 = Quaternion.LookRotation((GetComponent<SecretFinder>().target != null) ? (GetComponent<SecretFinder>().target.transform.position - playerCamera.transform.position) : (GameObject.Find("ObjectPos").transform.position - playerCamera.transform.position));
		if (secretLook)
		{
			playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, Quaternion.Euler(quaternion2.eulerAngles.x, base.transform.eulerAngles.y, 0f), compassLookSpeed * Time.deltaTime);
			base.transform.rotation = Quaternion.Slerp(Quaternion.Euler(base.transform.eulerAngles.x, base.transform.eulerAngles.y, base.transform.eulerAngles.z), Quaternion.Euler(0f, quaternion2.eulerAngles.y, 0f), compassLookSpeed * 0.8f * Time.deltaTime);
			secretTimer = 0.2f;
			return;
		}
		if (secretTimer >= 0f)
		{
			playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, Quaternion.Euler(initRotationX.eulerAngles.x, base.transform.eulerAngles.y, 0f), compassLookSpeed * 1.5f * Time.deltaTime);
			base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.Euler(0f, initRotationY, 0f), compassLookSpeed * Time.deltaTime);
			rotationZ = Mathf.Lerp(base.transform.localRotation.z, 0f, Time.deltaTime * smoothRotation);
			return;
		}
		if (compassLook)
		{
			playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, Quaternion.Euler(quaternion.eulerAngles.x, base.transform.eulerAngles.y, 0f), compassLookSpeed * Time.deltaTime);
			base.transform.rotation = Quaternion.Slerp(Quaternion.Euler(base.transform.eulerAngles.x, base.transform.eulerAngles.y, base.transform.eulerAngles.z), Quaternion.Euler(0f, quaternion.eulerAngles.y, 0f), compassLookSpeed * 0.8f * Time.deltaTime);
			compassTimer = 0.2f;
			return;
		}
		if (compassTimer >= 0f)
		{
			playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, Quaternion.Euler(initRotationX.eulerAngles.x, base.transform.eulerAngles.y, 0f), compassLookSpeed * 1.5f * Time.deltaTime);
			base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.Euler(0f, initRotationY, 0f), compassLookSpeed * Time.deltaTime);
			rotationZ = Mathf.Lerp(base.transform.localRotation.z, 0f, Time.deltaTime * smoothRotation);
			return;
		}
		rotationX -= Input.GetAxis("CameraHorizontal") * lookSpeedY * (float)invertYFactor;
		base.transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("CameraVertical") * lookSpeedX * (float)invertXFactor, 0f);
		rotationX -= Input.GetAxis("Mouse Y") * lookSpeedY * (float)invertYFactor;
		rotationX = Mathf.Clamp(rotationX, 0f - upperLookLimit, lowerLookLimit);
		playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, rotationZ);
		base.transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("Mouse X") * lookSpeedX * (float)invertXFactor, 0f);
		if (standing && playerCamera.transform.localPosition.y != defaultYPos && landTimer < 0f)
		{
			playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, characterController.center + new Vector3(0f, characterController.height / 2f, 0f) + offset, 5f * Time.deltaTime);
			headbobEndTimer = 3f;
		}
		else if (headbobEndTimer < 0f && headbobEndTimer > -0.25f)
		{
			playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, characterController.center + new Vector3(0f, characterController.height / 2f, 0f) + offset, 8f * Time.deltaTime);
		}
		else if (headbobEndTimer < 0.25f || !standing)
		{
			playerCamera.transform.localPosition = characterController.center + new Vector3(0f, characterController.height / 2f, 0f) + offset;
		}
	}

	private void HandleJump()
	{
		if (jumpSlope)
		{
			jumpTimer = 0f;
			jumpSlope = false;
			SoundManager.Instance.PlaySound(jumpClip);
			moveDirection.y = jumpForce;
			inJump = true;
			landing = true;
		}
		else if (aftervaultjumpTimer > 0f && (Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump")))
		{
			SoundManager.Instance.PlaySound(jumpClip);
			moveDirection.y = jumpForce;
			prejump = false;
			landing = true;
			aftervaultjumpTimer = 0f;
			inJump = true;
		}
		else if (prejump && characterController.isGrounded && prejumpCancelTimer > 0f)
		{
			SoundManager.Instance.PlaySound(jumpClip);
			moveDirection.y = jumpForce;
			prejump = false;
			landing = true;
			inJump = true;
		}
		else if (((Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump")) && characterController.isGrounded && airTime > 0.1f) || (airTime > 0.1f && (Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump")) && !inJump))
		{
			SoundManager.Instance.PlaySound(jumpClip);
			moveDirection.y = jumpForce;
			inJump = true;
			landing = true;
		}
	}

	private void HandleCrouch()
	{
		if (!characterController.isGrounded && (Input.GetKeyDown(crouchKey) || Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetButtonDown("Slide")))
		{
			preslide = true;
		}
		if (!characterController.isGrounded && (Input.GetKeyUp(crouchKey) || Input.GetKeyUp(KeyCode.C) || Input.GetKeyUp(KeyCode.LeftAlt) || Input.GetButtonUp("Slide")))
		{
			preslide = false;
		}
		slideTimer -= Time.deltaTime;
		keyUpTimer -= Time.deltaTime;
		if (standing && characterController.isGrounded)
		{
			tempslideCountdown -= Time.deltaTime;
		}
		if (!isCrouching || !crouchUpRay)
		{
			isCrouching = ShouldCrouch;
			if ((Input.GetKey(crouchKey) || Input.GetKey(KeyCode.C) || Input.GetButton("Slide") || Input.GetKey(KeyCode.LeftAlt)) && !characterController.isGrounded && !OnSlope())
			{
				InAirCrouch = true;
			}
			else
			{
				InAirCrouch = false;
			}
		}
		if (isCrouching)
		{
			crouchTimer = 0.35f;
		}
		if ((FlatSlide || (preslide && characterController.isGrounded)) && tempslideCountdown < 0f && verticalInputRaw > 0f && (Input.GetKey(crouchKey) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftAlt) || Input.GetButton("Slide")))
		{
			preslide = false;
			AddHorizontalForce(base.transform.forward / 12f, IsSprinting ? sprintSlideImpulsion : slideImpulsion);
			deceleration = (IsSprinting ? 2f : 3f);
			slideTimer = 3f;
			tempslideCountdown = slideCountdown;
			playerSource.Play();
		}
		else if (!characterController.isGrounded && slideTimer > 0f)
		{
			playerSource.Stop();
			deceleration = 9f;
		}
		else if ((Input.GetKeyUp(crouchKey) || Input.GetKeyUp(KeyCode.C) || Input.GetKeyUp(KeyCode.LeftAlt) || Input.GetButtonUp("Slide")) && characterController.isGrounded)
		{
			playerSource.Stop();
			slideTimer = 0.1f;
			forceFactor = 0f;
		}
		if ((Input.GetKeyUp(crouchKey) || Input.GetKeyUp(KeyCode.C) || Input.GetKeyUp(KeyCode.LeftAlt) || Input.GetButtonUp("Slide")) && !characterController.isGrounded)
		{
			keyUpTimer = 0.5f;
		}
	}

	private void LateUpdate()
	{
		crouchTimer -= Time.deltaTime;
		float num = ((isCrouching || InAirCrouch) ? crouchHeight : standHeight);
		if (characterController.height != num)
		{
			AdjustHeight(num);
		}
		if (crouchTimer < 0f && !ShouldCrouch)
		{
			standing = true;
		}
		else
		{
			standing = false;
		}
	}

	private void HandleCameraController()
	{
		if (IsSprinting)
		{
			isZooming = false;
		}
		else
		{
			isZooming = Input.GetButton("Fire2");
		}
		float num = (dialogueActive ? 70f : ((IsSprinting && standing && verticalInputRaw == 1f) ? runFOV : (isZooming ? zoomFOV : defaultFOV)));
		if (playerCamera.fieldOfView != num && !compassLook && compassTimer < 0f && !secretLook && secretTimer < 0f)
		{
			Zoom(num);
		}
		float temptiltAmount = ((InAirCrouch && !characterController.isGrounded) ? 0f : ((IsSprinting && standing && horizontalInputRaw == 1f) ? (0f - tiltAmount) : ((IsSprinting && standing && horizontalInputRaw == -1f) ? tiltAmount : 0f)));
		if (camController)
		{
			SideTilt(temptiltAmount);
		}
		_ = rotationZ;
	}

	private void HandleHeadbob()
	{
		if (!characterController.isGrounded && (landTimer > -0.06f || keyUpTimer > 0f))
		{
			fallTimer = 0f;
			playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, characterController.center + new Vector3(0f, characterController.height / 2f, 0f) + offset, 8f * Time.deltaTime);
		}
		else if (landTimer > 0f && !ShouldCrouch && !InAirCrouch && keyUpTimer < 0f)
		{
			if ((currentInputRaw != Vector2.zero) ? (fallTimer < 0.3f) : (fallTimer < 0.5f))
			{
				fallTimer += Time.deltaTime * fallBobSpeed;
			}
			else
			{
				fallTimer += Time.deltaTime * fallBobUpSpeed;
			}
			playerCamera.transform.localPosition = new Vector3(playerCamera.transform.localPosition.x, defaultYPos + Mathf.Sin(fallTimer) * fallBobAmount * 2f, playerCamera.transform.localPosition.z);
		}
		else if (currentInputRaw != new Vector2(0f, 0f) && landTimer < -0.4f && characterController.isGrounded && camController)
		{
			timer += Time.deltaTime * (isCrouching ? crouchBobSpeed : (IsSprinting ? sprintBobSpeed : walkBobSpeed));
			playerCamera.transform.localPosition = new Vector3(playerCamera.transform.localPosition.x, defaultYPos + Mathf.Sin(timer) * (isCrouching ? crouchBobAmount : (IsSprinting ? sprintBobAmount : walkBobAmount)), playerCamera.transform.localPosition.z);
		}
		else
		{
			fallTimer = 0f;
		}
	}

	private void HandleInteractionCheck()
	{
		RaycastHit hitInfo2;
		RaycastHit hitInfo3;
		if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out var hitInfo, maxSphereDistance, nomotoLayer) && motorBike.GetComponent<InteractObject>().onMoto)
		{
			currentHitObject = hitInfo.transform.gameObject;
			currentHitDistance = hitInfo.distance + sphereRadius;
		}
		else if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out hitInfo2, maxSphereDistance, inHandsLayer) && inHands)
		{
			currentHitObject = hitInfo2.transform.gameObject;
			currentHitDistance = hitInfo2.distance + sphereRadius;
		}
		else if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out hitInfo3, maxSphereDistance))
		{
			currentHitObject = hitInfo3.transform.gameObject;
			currentHitDistance = hitInfo3.distance + sphereRadius;
		}
		else
		{
			currentHitDistance = maxSphereDistance;
			currentHitObject = null;
		}
		if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out var hitInfo4, maxInteractionDistance))
		{
			if (hitInfo4.transform.gameObject.layer == 7)
			{
				currentHitObject = hitInfo4.transform.gameObject;
			}
			interactionDistance = hitInfo4.distance;
		}
		else
		{
			interactionDistance = maxInteractionDistance;
		}
		RaycastHit hitInfo6;
		RaycastHit hitInfo7;
		RaycastHit hitInfo8;
		RaycastHit hitInfo9;
		RaycastHit hitInfo10;
		RaycastHit hitInfo11;
		RaycastHit hitInfo12;
		if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out var hitInfo5, interactionDistance, motoLayer) && !motorBike.GetComponent<InteractObject>().onMoto && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo5.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo5.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
		}
		else if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out hitInfo6, interactionDistance, interactionLayer) && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo6.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo6.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
			talkPopup.SetActive(value: false);
			jukeboxPopup.SetActive(value: false);
			doorPopup.SetActive(value: false);
		}
		else if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out hitInfo7, interactionDistance, doorLayer) && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo7.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo7.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
			talkPopup.SetActive(value: false);
			grabPopup.SetActive(value: false);
		}
		else if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out hitInfo8, interactionDistance, npcLayer) && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo8.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo8.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
			grabPopup.SetActive(value: false);
			doorPopup.SetActive(value: false);
			jukeboxPopup.SetActive(value: false);
		}
		else if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out hitInfo9, currentHitDistance, motoLayer) && !motorBike.GetComponent<InteractObject>().onMoto && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo9.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo9.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
		}
		else if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out hitInfo10, currentHitDistance, interactionLayer) && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo10.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo10.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
			talkPopup.SetActive(value: false);
			jukeboxPopup.SetActive(value: false);
			doorPopup.SetActive(value: false);
		}
		else if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out hitInfo11, currentHitDistance, doorLayer) && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo11.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo11.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
			talkPopup.SetActive(value: false);
			grabPopup.SetActive(value: false);
		}
		else if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out hitInfo12, currentHitDistance, npcLayer) && !dialogueActive)
		{
			interactionSphere = true;
			hitInfo12.collider.TryGetComponent<Interactable>(out currentInteractable);
			currentObject = hitInfo12.collider.gameObject;
			if ((bool)currentInteractable)
			{
				currentInteractable.OnFocus();
			}
			grabPopup.SetActive(value: false);
			doorPopup.SetActive(value: false);
			jukeboxPopup.SetActive(value: false);
		}
		else if ((bool)currentInteractable)
		{
			interactionSphere = false;
			currentInteractable.OnLoseFocus();
			currentInteractable = null;
			currentObject = null;
			noticketPopup.SetActive(value: false);
			grabPopup.SetActive(value: false);
			talkPopup.SetActive(value: false);
			jukeboxPopup.SetActive(value: false);
			elevatorPopup.SetActive(value: false);
			doorPopup.SetActive(value: false);
		}
		if (currentInteractable == null)
		{
			interactionSphere = false;
			noticketPopup.SetActive(value: false);
			grabPopup.SetActive(value: false);
			talkPopup.SetActive(value: false);
			jukeboxPopup.SetActive(value: false);
			elevatorPopup.SetActive(value: false);
			doorPopup.SetActive(value: false);
		}
		RaycastHit hitInfo13;
		RaycastHit hitInfo14;
		if (dialogueActive)
		{
			canThrow = false;
		}
		else if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRayPoint), out hitInfo13, interactionDistance, allinteractLayer))
		{
			if (hitInfo13.collider.tag == "DontThrow")
			{
				canThrow = false;
			}
			else if ((isCrouching && headRay) || throwRay)
			{
				canThrow = false;
			}
			else
			{
				canThrow = true;
			}
		}
		else if (Physics.SphereCast(playerCamera.transform.position, sphereRadius, playerCamera.transform.forward, out hitInfo14, currentHitDistance, allinteractLayer))
		{
			if (hitInfo14.collider.tag == "DontThrow")
			{
				canThrow = false;
			}
			else if ((isCrouching && headRay) || throwRay)
			{
				canThrow = false;
			}
			else
			{
				canThrow = true;
			}
		}
		else if ((isCrouching && headRay) || throwRay)
		{
			canThrow = false;
		}
		else
		{
			canThrow = true;
		}
	}

	private void HandleInteractionInput()
	{
		if ((Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && currentInteractable != null && interactionSphere && !dialogueActive)
		{
			currentInteractable.OnInteract();
		}
	}

	private void ApplyFinalMovements()
	{
		if ((currentInputRaw.magnitude != 0f || motorBike.GetComponent<InteractObject>().onMoto) && OnSlope() && (GameObject.Find("GameManager").GetComponent<GameManager>().gamepad ? (!Input.GetButton("Jump")) : (!Input.GetKey(jumpKey))))
		{
			if (Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump"))
			{
				moveDirection.y = 0f;
				jumpSlope = true;
			}
			else
			{
				jumpSlope = false;
			}
			if (!jumpSlope)
			{
				moveDirection.y -= gravity * slopeForce * Time.deltaTime;
			}
			leaveSlope = true;
		}
		else if (!characterController.isGrounded)
		{
			moveDirection.y -= gravity * Time.deltaTime;
		}
		if (backRay && characterController.velocity.magnitude > 30f)
		{
			moveDirection.x /= 4f;
			moveDirection.z /= 4f;
			force.x = 0f;
			force.z = 0f;
		}
		if (upRay && !characterController.isGrounded)
		{
			AddVerticalForce(new Vector3(0f, -1f, 0f), 2f);
		}
		if (OnCeilingSlope())
		{
			AddVerticalForce(new Vector3(0f, -1f, 0f), IsSprinting ? (-8f) : (-5f));
			moveDirection.y = (IsSprinting ? (-8f) : (-5f));
		}
		if (WillSlideOnSlopes && CrouchSliding && GetComponent<Slope>().surfaceAngle >= 12f && !Input.GetKey(jumpKey) && !Input.GetButton("Jump"))
		{
			moveDirection += new Vector3(hitPointNormal.x, 0f - hitPointNormal.y, hitPointNormal.z) * slopeSlideSpeed * hitPointNormal.magnitude;
			moveDirection.y -= 800f * Time.deltaTime;
		}
		characterController.Move(moveDirection * Time.deltaTime * currentWeight);
	}

	private void HandleFootsteps()
	{
		if (!characterController.isGrounded || currentInputRaw == Vector2.zero)
		{
			return;
		}
		footstepTimer -= Time.deltaTime;
		if (!(footstepTimer <= 0f) || isCrouching || !(currentInputRaw != Vector2.zero))
		{
			return;
		}
		if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, 3f))
		{
			switch (hitInfo.collider.tag)
			{
			case "Footsteps/Metal":
				SoundManager.Instance.PlaySound(metalClips[UnityEngine.Random.Range(0, metalClips.Length - 1)]);
				break;
			case "Footsteps/Taule":
				SoundManager.Instance.PlaySound(tauleClips[UnityEngine.Random.Range(0, tauleClips.Length - 1)]);
				break;
			case "Footsteps/Wood":
				SoundManager.Instance.PlaySound(woodClips[UnityEngine.Random.Range(0, woodClips.Length - 1)]);
				break;
			case "Footsteps/Concrete":
				SoundManager.Instance.PlaySound(concreteClips[UnityEngine.Random.Range(0, concreteClips.Length - 1)]);
				break;
			case "Footsteps/Stone":
				SoundManager.Instance.PlaySound(stoneClips[UnityEngine.Random.Range(0, stoneClips.Length - 1)]);
				break;
			case "Footsteps/Grass":
				SoundManager.Instance.PlaySound(grassClips[UnityEngine.Random.Range(0, grassClips.Length - 1)]);
				break;
			case "Footsteps/Dirt":
				SoundManager.Instance.PlaySound(dirtClips[UnityEngine.Random.Range(0, dirtClips.Length - 1)]);
				break;
			case "Footsteps/Sand":
				SoundManager.Instance.PlaySound(sandClips[UnityEngine.Random.Range(0, sandClips.Length - 1)]);
				break;
			case "Footsteps/Water":
				SoundManager.Instance.PlaySound(waterClips[UnityEngine.Random.Range(0, waterClips.Length - 1)]);
				break;
			default:
				SoundManager.Instance.PlaySound(stoneClips[UnityEngine.Random.Range(0, stoneClips.Length - 1)]);
				break;
			}
		}
		footstepTimer = GetCurrentOffset;
	}

	private void AdjustHeight(float height)
	{
		float y = height / 2f;
		characterController.height = Mathf.Lerp(characterController.height, height, crouchingSpeed * Time.deltaTime);
		characterController.center = Vector3.Lerp(characterController.center, new Vector3(0f, y, 0f), crouchingSpeed * Time.deltaTime);
	}

	private void Zoom(float fov)
	{
		playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, fov, zoomSpeed * Time.deltaTime);
	}

	private void SideTilt(float temptiltAmount)
	{
		float b = Mathf.Clamp((0f - Input.GetAxis("Mouse X")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
		rotationZ = Mathf.Lerp(rotationZ, temptiltAmount, tiltSpeed * Time.deltaTime) + Mathf.Lerp(base.transform.localRotation.z, b, Time.deltaTime * smoothRotation);
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = UnityEngine.Color.red;
		UnityEngine.Debug.DrawLine(playerCamera.transform.position, playerCamera.transform.position + playerCamera.transform.forward * currentHitDistance);
		Gizmos.DrawWireSphere(playerCamera.transform.position + playerCamera.transform.forward * currentHitDistance, sphereRadius);
		UnityEngine.Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * interactionDistance, UnityEngine.Color.green);
	}
}
public class FlashLight : MonoBehaviour
{
	public GameObject light;

	private void Start()
	{
	}

	private void Update()
	{
		if (GetComponent<InteractObject>().inHands && (Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Fire1")))
		{
			light.SetActive(!light.activeSelf);
		}
	}
}
public class Grabber : MonoBehaviour
{
	public bool active;

	[SerializeField]
	public GameObject arrow;

	[SerializeField]
	public GameObject arrowInitPosition;

	private void Start()
	{
	}

	private void Update()
	{
		if (base.gameObject.transform.childCount > 1)
		{
			active = false;
		}
		else
		{
			active = true;
		}
	}
}
public class Guidon : MonoBehaviour
{
	[Header("Rotation")]
	public float rotationAmount = 4f;

	public float maxRotationAmount = 5f;

	public float smoothRotation = 12f;

	[Space]
	public bool rotationX = true;

	public bool rotationY = true;

	public bool rotationZ = true;

	private void Start()
	{
	}

	private void FixedUpdate()
	{
		if (base.transform.parent.GetComponent<InteractObject>().inHands)
		{
			TiltSway(Quaternion.Euler(0f, GameObject.Find("MotoPos").transform.rotation.y, 32.997f));
		}
	}

	public void TiltSway(Quaternion initialRotation)
	{
		float num = Mathf.Clamp((0f - Input.GetAxis("Mouse X")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
		float num2 = Mathf.Clamp((0f - Input.GetAxis("Mouse Y")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
		Quaternion quaternion = Quaternion.Euler(new Vector3(rotationX ? num2 : 0f, rotationY ? num : 0f, rotationZ ? num : 0f));
		base.transform.localRotation = Quaternion.Slerp(base.transform.localRotation, quaternion * initialRotation, Time.deltaTime * smoothRotation);
	}

	public void TiltSwayGlobal(Quaternion initialRotation)
	{
		if (GameObject.Find("GameManager").GetComponent<GameManager>().gamepad)
		{
			float num = Mathf.Clamp((0f - Input.GetAxis("CameraHorizontal")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			float num2 = Mathf.Clamp((0f - Input.GetAxis("CameraVertical")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			Quaternion quaternion = Quaternion.Euler(new Vector3(rotationX ? num2 : 0f, rotationY ? num2 : 0f, rotationZ ? num : 0f));
			base.transform.rotation = Quaternion.Slerp(base.transform.rotation, quaternion * initialRotation, Time.deltaTime * smoothRotation);
		}
		else
		{
			float num3 = Mathf.Clamp((0f - Input.GetAxis("Mouse X")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			float num4 = Mathf.Clamp((0f - Input.GetAxis("Mouse Y")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			Quaternion quaternion2 = Quaternion.Euler(new Vector3(rotationX ? num4 : 0f, rotationY ? num4 : 0f, rotationZ ? num3 : 0f));
			base.transform.rotation = Quaternion.Slerp(base.transform.rotation, quaternion2 * initialRotation, Time.deltaTime * smoothRotation);
		}
	}
}
public class Gun : MonoBehaviour
{
	public int gunId;

	[Header("Values")]
	public float damage = 10f;

	public float range = 100f;

	public float fireRate = 15f;

	public float impactForce = 30f;

	private float nextTimeToFire;

	private Vector3 spread = new Vector3(0f, 0f, 0f);

	public int maxAmmo = 16;

	public int currentAmmo;

	public int ammoStock;

	public float reloadTime = 0.8f;

	private bool isReloading;

	public Animator animator;

	public Camera fpsCam;

	[Header("SFX")]
	public AudioClip shoot;

	public AudioClip impact;

	public AudioClip reload;

	public AudioClip woodImpact;

	public AudioClip stoneImpact;

	public AudioClip grassImpact;

	[Header("VFX")]
	public ParticleSystem muzzleFlash;

	public GameObject impactEffect;

	public GameObject woodEffect;

	public GameObject stoneEffect;

	public GameObject grassEffect;

	public GameObject bloodEffect;

	public GameObject enemyDeadEffect;

	[Header("Hole")]
	public GameObject impactHole;

	public GameObject woodHole;

	public GameObject stoneHole;

	public GameObject grassHole;

	private void Start()
	{
	}

	private void OnEnable()
	{
		isReloading = false;
	}

	private void Update()
	{
		animator.SetBool("Reloading", isReloading);
		GameObject.Find("AmmoStock").GetComponent<TextMeshProUGUI>().text = ammoStock.ToString();
		GameObject.Find("CurrentAmmo").GetComponent<TextMeshProUGUI>().text = currentAmmo.ToString();
		if (isReloading)
		{
			return;
		}
		if (Input.GetKeyDown(KeyCode.R) && currentAmmo != maxAmmo && ammoStock > 0)
		{
			StartCoroutine(Reload());
			return;
		}
		if (currentAmmo <= 0 && currentAmmo != maxAmmo && ammoStock > 0)
		{
			StartCoroutine(Reload());
			return;
		}
		if (Input.GetButtonDown("Fire1") && currentAmmo > 0)
		{
			Shoot();
			nextTimeToFire = Time.time + 1f / fireRate;
		}
		else if (Input.GetButton("Fire1") && Time.time >= nextTimeToFire && currentAmmo > 0)
		{
			nextTimeToFire = Time.time + 1f / fireRate;
			Shoot();
		}
		if (Physics.Raycast(fpsCam.transform.position, fpsCam.transform.forward, out var hitInfo, range))
		{
			if (hitInfo.transform.tag == "Footsteps/Wood" || hitInfo.transform.tag == "Object/Wood")
			{
				impactEffect = woodEffect;
				impact = woodImpact;
				impactHole = woodHole;
			}
			if (hitInfo.transform.tag == "Footsteps/Stone" || hitInfo.transform.tag == "Object/Stone")
			{
				impactEffect = stoneEffect;
				impact = stoneImpact;
				impactHole = stoneHole;
			}
			if (hitInfo.transform.tag == "Footsteps/Grass")
			{
				impactEffect = grassEffect;
				impact = grassImpact;
				impactHole = grassHole;
			}
			if (hitInfo.transform.tag == "Enemy")
			{
				impactEffect = bloodEffect;
				impact = stoneImpact;
			}
		}
	}

	private IEnumerator Reload()
	{
		isReloading = true;
		SoundManager.Instance.PlaySound(reload);
		yield return new WaitForSeconds(reloadTime);
		if (ammoStock - maxAmmo >= 0)
		{
			ammoStock -= maxAmmo - currentAmmo;
			currentAmmo = maxAmmo;
		}
		else
		{
			currentAmmo = ammoStock;
			ammoStock = 0;
		}
		isReloading = false;
	}

	private void Shoot()
	{
		muzzleFlash.Play();
		SoundManager.Instance.PlaySound(shoot);
		currentAmmo--;
		if (gunId == 0)
		{
			spread = fpsCam.transform.forward;
		}
		if (gunId == 1)
		{
			spread = fpsCam.transform.forward;
		}
		if (gunId == 2)
		{
			spread = fpsCam.transform.forward + new Vector3(UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(-0.1f, 0.1f));
		}
		if (Physics.Raycast(fpsCam.transform.position, spread, out var hitInfo, range) && hitInfo.collider.tag != "Zone")
		{
			UnityEngine.Debug.Log(hitInfo.transform.tag);
			Target component = hitInfo.transform.GetComponent<Target>();
			if (component != null)
			{
				component.TakeDamage(damage);
				UnityEngine.Object.Instantiate(enemyDeadEffect, hitInfo.transform.position, Quaternion.LookRotation(hitInfo.normal));
			}
			if (hitInfo.rigidbody != null)
			{
				hitInfo.rigidbody.AddForce(-hitInfo.normal * impactForce);
			}
			SoundManager.Instance.PlaySound(impact);
			UnityEngine.Object.Instantiate(impactEffect, hitInfo.point, Quaternion.LookRotation(hitInfo.normal));
			if (hitInfo.transform.tag == "Object/Wood" || hitInfo.transform.tag == "Object/Stone")
			{
				UnityEngine.Object.Instantiate(impactHole, hitInfo.point, Quaternion.LookRotation(hitInfo.normal), hitInfo.transform);
			}
			else if (hitInfo.transform.tag != "Enemy")
			{
				UnityEngine.Object.Instantiate(impactHole, hitInfo.point, Quaternion.LookRotation(hitInfo.normal));
			}
		}
	}
}
public class Map : MonoBehaviour
{
	private void Start()
	{
		if (GameObject.Find("GameManager").GetComponent<GameManager>().haveMap == 1)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void Update()
	{
	}
}
public class MapDisplay : MonoBehaviour
{
	[SerializeField]
	private Sprite bigMap;

	[SerializeField]
	private Sprite smallMap;

	[SerializeField]
	private GameObject mapButton;

	public bool mapBig = true;

	private void Start()
	{
	}

	private void Update()
	{
		if (Input.GetButtonDown("Fire1") && !mapButton.GetComponent<MapButton>().mouseOver)
		{
			mapBig = !mapBig;
		}
		if (mapBig)
		{
			GetComponent<UnityEngine.UI.Image>().sprite = bigMap;
		}
		else if (!mapBig)
		{
			GetComponent<UnityEngine.UI.Image>().sprite = smallMap;
		}
	}
}
public class MotorBike : MonoBehaviour
{
	private GameObject player;

	[SerializeField]
	private AudioSource audio;

	public Vector3 motorMovement;

	public float power = 2f;

	public float acceleration = 10f;

	public bool isActive;

	public GameObject guidon;

	private Vector3 desiredSpeed;

	public float mouseLook;

	public float timeToImpress = 3f;

	public float impressTimer;

	[SerializeField]
	private float tiltAmount = 7f;

	[SerializeField]
	private float tiltSpeed = 7f;

	[SerializeField]
	private AudioClip idle;

	[SerializeField]
	private AudioClip run;

	private void Awake()
	{
		player = GameObject.Find("Player");
	}

	private void Start()
	{
		audio.Play();
	}

	private void Update()
	{
		if ((Input.GetButton("Fire1") || Input.GetAxis("LeftClick") > 0.1f) && GetComponent<InteractObject>().onMoto)
		{
			isActive = true;
		}
		else
		{
			isActive = false;
		}
		if (Input.GetButtonDown("Fire1"))
		{
			audio.clip = run;
		}
		if (Input.GetButtonUp("Fire1"))
		{
			audio.clip = idle;
		}
		if (!player.GetComponent<FirstPersonController>().pause)
		{
			if (!GetComponent<InteractObject>().inHands)
			{
				audio.Stop();
			}
			else
			{
				if (Input.GetButtonDown("Fire1"))
				{
					audio.Play();
				}
				if (Input.GetButtonUp("Fire1"))
				{
					audio.Play();
				}
			}
		}
		if (player.GetComponent<FirstPersonController>().pause)
		{
			audio.Stop();
		}
		if (!player.GetComponent<FirstPersonController>().characterController.isGrounded && GetComponent<InteractObject>().inHands)
		{
			mouseLook -= Input.GetAxis("CameraHorizontal") * GameObject.Find("Player").GetComponent<FirstPersonController>().lookSpeedX;
			mouseLook -= Input.GetAxis("Mouse X") * GameObject.Find("Player").GetComponent<FirstPersonController>().lookSpeedX;
			if (mouseLook > 360f || mouseLook < -360f)
			{
				if (!GameObject.Find("GameManager").GetComponent<GameManager>().bikeAir)
				{
					GameObject.Find("GameManager").GetComponent<GameManager>().Popup();
				}
				GameObject.Find("GameManager").GetComponent<GameManager>().bikeAir = true;
				GameObject.Find("GameManager").GetComponent<GameManager>().bikeAirState = 1;
				GameObject.Find("GameManager").GetComponent<GameManager>().lastAchievement = "Way of the biker";
				SteamIntegration.Instance.UnlockAchivement("WayOfTheBiker");
			}
		}
		else
		{
			mouseLook = 0f;
		}
		if (player.GetComponent<EnterZone>().inGirlZone && GetComponent<InteractObject>().inHands)
		{
			if (motorMovement.magnitude > (new Vector3(player.transform.forward.x, 0f, player.transform.forward.z) * power).magnitude / 4f)
			{
				impressTimer += Time.deltaTime;
				if (impressTimer > timeToImpress)
				{
					impressTimer = -100f;
					if (!GameObject.Find("GameManager").GetComponent<GameManager>().impressGirl)
					{
						GameObject.Find("GameManager").GetComponent<GameManager>().Popup();
					}
					GameObject.Find("GameManager").GetComponent<GameManager>().impressGirl = true;
					GameObject.Find("GameManager").GetComponent<GameManager>().impressGirlState = 1;
					GameObject.Find("GameManager").GetComponent<GameManager>().lastAchievement = "Way of the seducer";
					SteamIntegration.Instance.UnlockAchivement("WayOfTheSeducer");
				}
			}
			else
			{
				impressTimer = 0f;
			}
		}
		if (player.GetComponent<FirstPersonController>().characterController.isGrounded)
		{
			desiredSpeed = (isActive ? (new Vector3(player.transform.forward.x, 0f, player.transform.forward.z) * power) : Vector3.zero);
		}
		if (motorMovement != desiredSpeed && player.GetComponent<FirstPersonController>().characterController.isGrounded)
		{
			GetSpeed(desiredSpeed);
		}
	}

	private void GetSpeed(Vector3 tempspeed)
	{
		motorMovement = Vector3.Lerp(motorMovement, tempspeed, acceleration * Time.deltaTime);
	}
}
public class Piano : Interactable
{
	private float mouseX;

	private float mouseY;

	private float timer;

	private bool active;

	private Vector3 initPos;

	private Vector3 pressPos;

	[SerializeField]
	private AudioClip pianoSound;

	private new void Awake()
	{
		initPos = base.transform.position;
		pressPos = new Vector3(initPos.x, initPos.y - 0.05f, initPos.z);
	}

	public override void OnFocus()
	{
		active = true;
	}

	public override void OnInteract()
	{
	}

	public override void OnLoseFocus()
	{
		active = false;
		base.transform.position = Vector3.Lerp(base.transform.position, initPos, 12f * Time.deltaTime);
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (GameObject.Find("GameManager").GetComponent<GameManager>().gamepad)
		{
			mouseX = Input.GetAxis("CameraHorizontal");
			mouseY = Input.GetAxis("CameraVertical");
		}
		else
		{
			mouseX = Input.GetAxis("Mouse X");
			mouseY = Input.GetAxis("Mouse Y");
		}
		if (GameObject.Find("Player").GetComponent<FirstPersonController>().currentObject == base.gameObject && active)
		{
			if (Input.GetButton("Fire1") || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.F))
			{
				base.transform.position = Vector3.Lerp(base.transform.position, pressPos, 12f * Time.deltaTime);
			}
			else
			{
				base.transform.position = Vector3.Lerp(base.transform.position, initPos, 12f * Time.deltaTime);
			}
			float magnitude = new Vector2(mouseX, mouseY).magnitude;
			if ((Input.GetButton("Fire1") || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.F)) && magnitude > 0.3f && timer < 0f)
			{
				timer = 0.2f;
				SoundManager.Instance.PlaySound(pianoSound);
			}
			else if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
			{
				SoundManager.Instance.PlaySound(pianoSound);
			}
		}
		if (GameObject.Find("Player").GetComponent<FirstPersonController>().currentObject != base.gameObject)
		{
			base.transform.position = Vector3.Lerp(base.transform.position, initPos, 12f * Time.deltaTime);
		}
	}
}
public class PickupItem : MonoBehaviour
{
	public GameObject club;

	public GameObject climber;

	public GameObject propeller;

	public GameObject blower;

	public GameObject flashlight;

	public GameObject soap;

	public GameObject ball;

	public GameObject bigball;

	public GameObject stick;

	public GameObject grabber;

	public GameObject motorBike;

	public GameObject compass;

	public GameObject trumpet;

	public GameObject secretfinder;

	private GameManager g;

	private void Awake()
	{
		g = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Start()
	{
		if (g.soap == 1)
		{
			UnityEngine.Object.Instantiate(soap, GameObject.Find("ObjectPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
		if (g.club == 1)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(club, GameObject.Find("ObjectPos2").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
			gameObject.GetComponent<InteractObject>().inHands = true;
			GameObject.Find("Player").GetComponent<FirstPersonController>().club = gameObject;
		}
		if (g.climber == 1)
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(climber, GameObject.Find("Pickaxe01").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
			gameObject2.GetComponent<InteractObject>().inHands = true;
			GameObject.Find("Player").GetComponent<FirstPersonController>().climber = gameObject2;
		}
		if (g.flashlight == 1)
		{
			UnityEngine.Object.Instantiate(flashlight, GameObject.Find("LightPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
		if (g.propeller == 1)
		{
			GameObject gameObject3 = UnityEngine.Object.Instantiate(propeller, GameObject.Find("PropellerPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
			gameObject3.GetComponent<InteractObject>().inHands = true;
			GameObject.Find("Player").GetComponent<FirstPersonController>().propeller = gameObject3;
		}
		if (g.blower == 1)
		{
			GameObject gameObject4 = UnityEngine.Object.Instantiate(blower, GameObject.Find("BlowerPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
			gameObject4.GetComponent<InteractObject>().inHands = true;
			GameObject.Find("Player").GetComponent<FirstPersonController>().blower = gameObject4;
		}
		if (g.ball == 1)
		{
			UnityEngine.Object.Instantiate(ball, GameObject.Find("ObjectPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
		if (g.bigball == 1)
		{
			UnityEngine.Object.Instantiate(bigball, GameObject.Find("BigBallPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
		if (g.stick == 1)
		{
			UnityEngine.Object.Instantiate(stick, GameObject.Find("StickPos01").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
		if (g.grabber == 1)
		{
			GameObject gameObject5 = UnityEngine.Object.Instantiate(grabber, GameObject.Find("GrabberPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
			gameObject5.GetComponent<InteractObject>().inHands = true;
			GameObject.Find("Player").GetComponent<FirstPersonController>().grabber = gameObject5;
		}
		if (g.motorBike == 1)
		{
			GameObject gameObject6 = UnityEngine.Object.Instantiate(motorBike, GameObject.Find("MotoPos").transform.position, Quaternion.identity, GameObject.Find("Player").transform);
			gameObject6.GetComponent<InteractObject>().inHands = true;
			GameObject.Find("Player").GetComponent<FirstPersonController>().motorBike = gameObject6;
		}
		if (g.compass == 1)
		{
			UnityEngine.Object.Instantiate(compass, GameObject.Find("CompassPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
		if (g.trumpet == 1)
		{
			UnityEngine.Object.Instantiate(trumpet, GameObject.Find("TrumpetPos00").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
		if (g.secretfinder == 1)
		{
			UnityEngine.Object.Instantiate(secretfinder, GameObject.Find("SecretFinderPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
		}
	}

	public void GetCompass()
	{
		UnityEngine.Object.Instantiate(compass, GameObject.Find("CompassPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
	}

	private void Update()
	{
		if (g.pickup)
		{
			g.pickup = false;
			if (g.item == 0)
			{
				UnityEngine.Object.Instantiate(soap, GameObject.Find("ObjectPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
			if (g.item == 1)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(club, GameObject.Find("ObjectPos2").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
				gameObject.GetComponent<InteractObject>().inHands = true;
				GameObject.Find("Player").GetComponent<FirstPersonController>().club = gameObject;
			}
			if (g.item == 2)
			{
				GameObject gameObject2 = UnityEngine.Object.Instantiate(climber, GameObject.Find("Pickaxe01").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
				gameObject2.GetComponent<InteractObject>().inHands = true;
				GameObject.Find("Player").GetComponent<FirstPersonController>().climber = gameObject2;
			}
			if (g.item == 4)
			{
				UnityEngine.Object.Instantiate(flashlight, GameObject.Find("LightPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
			if (g.item == 5)
			{
				GameObject gameObject3 = UnityEngine.Object.Instantiate(propeller, GameObject.Find("PropellerPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
				gameObject3.GetComponent<InteractObject>().inHands = true;
				GameObject.Find("Player").GetComponent<FirstPersonController>().propeller = gameObject3;
			}
			if (g.item == 6)
			{
				GameObject gameObject4 = UnityEngine.Object.Instantiate(blower, GameObject.Find("BlowerPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
				gameObject4.GetComponent<InteractObject>().inHands = true;
				GameObject.Find("Player").GetComponent<FirstPersonController>().blower = gameObject4;
			}
			if (g.item == 7)
			{
				UnityEngine.Object.Instantiate(ball, GameObject.Find("ObjectPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
			if (g.item == 8)
			{
				UnityEngine.Object.Instantiate(bigball, GameObject.Find("BigBallPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
			if (g.item == 9)
			{
				UnityEngine.Object.Instantiate(stick, GameObject.Find("StickPos01").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
			if (g.item == 10)
			{
				GameObject gameObject5 = UnityEngine.Object.Instantiate(grabber, GameObject.Find("GrabberPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform);
				gameObject5.GetComponent<InteractObject>().inHands = true;
				GameObject.Find("Player").GetComponent<FirstPersonController>().grabber = gameObject5;
			}
			if (g.item == 11)
			{
				GameObject gameObject6 = UnityEngine.Object.Instantiate(motorBike, GameObject.Find("MotoPos").transform.position, Quaternion.identity, GameObject.Find("Player").transform);
				gameObject6.GetComponent<InteractObject>().inHands = true;
				GameObject.Find("Player").GetComponent<FirstPersonController>().motorBike = gameObject6;
			}
			if (g.item == 12)
			{
				UnityEngine.Object.Instantiate(compass, GameObject.Find("CompassPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
			if (g.item == 13)
			{
				UnityEngine.Object.Instantiate(trumpet, GameObject.Find("TrumpetPos00").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
			if (g.item == 16)
			{
				UnityEngine.Object.Instantiate(secretfinder, GameObject.Find("SecretFinderPos").transform.position, Quaternion.identity, GameObject.Find("Main Camera").transform).GetComponent<InteractObject>().inHands = true;
			}
		}
	}
}
public class PlayerStats : MonoBehaviour
{
	public float maxHealth = 100f;

	public float health = 100f;

	private float invincibiltyTimer;

	private void Start()
	{
	}

	private void Update()
	{
		invincibiltyTimer -= Time.deltaTime;
		GameObject.Find("Health").GetComponent<TextMeshProUGUI>().text = health + " HP";
	}

	private void OnTriggerStay(Collider collisionInfo)
	{
		if (collisionInfo.tag == "Enemy" || collisionInfo.tag == "Projectile")
		{
			if (invincibiltyTimer < 0f)
			{
				health -= collisionInfo.GetComponent<Target>().damage;
				invincibiltyTimer = 0.5f;
			}
			if (collisionInfo.tag == "Projectile")
			{
				UnityEngine.Object.Destroy(collisionInfo.gameObject, 0.1f);
			}
		}
	}
}
public class Propeller : MonoBehaviour
{
	public GameObject motor;

	public GameObject motorUp;

	private GameObject player;

	[SerializeField]
	private AudioSource audio;

	[SerializeField]
	private AudioSource audio1;

	public Vector3 propellerMovement;

	public float power = 2f;

	public float acceleration = 10f;

	public float flypower = 0.8f;

	public float rotateSpeed = 100f;

	public float rotation;

	public bool isActive;

	private bool turn;

	private void Awake()
	{
		player = GameObject.Find("Player");
	}

	private void Update()
	{
		if (!player.GetComponent<FirstPersonController>().pause)
		{
			if ((Input.GetKey(KeyCode.Space) || Input.GetButton("Jump")) && GetComponent<InteractObject>().inHands && player.transform.position.y < 120f)
			{
				motorUp.transform.Rotate(1000f * Time.deltaTime, 0f, 0f);
				player.GetComponent<FirstPersonController>().moveDirection.y += flypower * Time.deltaTime;
				audio1.mute = false;
			}
			else
			{
				audio1.mute = true;
			}
			motor.transform.Rotate(0f, rotation * Time.deltaTime, 0f);
			if ((Input.GetButton("Fire1") || Input.GetAxis("LeftClick") > 0.1f) && GetComponent<InteractObject>().inHands && player.transform.position.y < 120f)
			{
				isActive = true;
				rotation = rotateSpeed;
				turn = true;
				player.GetComponent<FirstPersonController>().moveDirection.y += propellerMovement.y * Time.deltaTime;
			}
			else
			{
				isActive = false;
				if (turn)
				{
					rotation -= 300f * Time.deltaTime;
				}
				if (rotation < 0f)
				{
					turn = false;
				}
			}
			if (!player.GetComponent<FirstPersonController>().pause && player.transform.position.y < 120f)
			{
				if (Input.GetButtonDown("Fire1"))
				{
					audio.mute = false;
				}
				if (!isActive)
				{
					audio.mute = true;
				}
			}
			else if (!isActive)
			{
				audio.mute = true;
			}
			Vector3 vector = (isActive ? (player.GetComponent<FirstPersonController>().playerCamera.transform.forward * power) : Vector3.zero);
			if (propellerMovement != vector)
			{
				GetSpeed(vector);
			}
		}
		else
		{
			audio.mute = true;
			audio1.mute = true;
		}
	}

	private void GetSpeed(Vector3 tempspeed)
	{
		propellerMovement = Vector3.Lerp(propellerMovement, tempspeed, acceleration * Time.deltaTime);
	}
}
public class SecretFinder : MonoBehaviour
{
	[SerializeField]
	private GameObject[] secrets;

	private int index;

	public Transform target;

	private void Start()
	{
		index = UnityEngine.Random.Range(0, secrets.Length - 1);
	}

	private void Update()
	{
		if (secrets[index] != null)
		{
			if (secrets[index].activeSelf)
			{
				target = secrets[index].transform;
			}
			else
			{
				index = UnityEngine.Random.Range(0, secrets.Length);
			}
		}
		else
		{
			index = UnityEngine.Random.Range(0, secrets.Length);
		}
	}
}
public class Slope : MonoBehaviour
{
	public Transform rearRayPos;

	public Transform frontRayPos;

	public LayerMask layerMask;

	public float surfaceAngle;

	public bool uphill;

	public bool downhill;

	public bool flatSurface;

	private void Update()
	{
		rearRayPos.rotation = Quaternion.Euler(0f - base.gameObject.transform.rotation.x, 0f - base.gameObject.transform.rotation.y, 0f - base.gameObject.transform.rotation.z);
		frontRayPos.rotation = Quaternion.Euler(0f - base.gameObject.transform.rotation.x, 0f - base.gameObject.transform.rotation.y, 0f - base.gameObject.transform.rotation.z);
		if (Physics.Raycast(rearRayPos.position, rearRayPos.TransformDirection(-Vector3.up), out var hitInfo, float.PositiveInfinity, layerMask))
		{
			UnityEngine.Debug.DrawRay(rearRayPos.position, rearRayPos.TransformDirection(-Vector3.up) * hitInfo.distance, UnityEngine.Color.yellow);
			surfaceAngle = Vector3.Angle(hitInfo.normal, Vector3.up);
		}
		else
		{
			UnityEngine.Debug.DrawRay(rearRayPos.position, rearRayPos.TransformDirection(-Vector3.up) * 1000f, UnityEngine.Color.red);
			uphill = false;
		}
		Vector3 vector = new Vector3(frontRayPos.position.x, rearRayPos.position.y, rearRayPos.position.z);
		if (Physics.Raycast(vector, frontRayPos.TransformDirection(-Vector3.up), out var hitInfo2, float.PositiveInfinity, layerMask))
		{
			UnityEngine.Debug.DrawRay(vector, frontRayPos.TransformDirection(-Vector3.up) * hitInfo2.distance, UnityEngine.Color.yellow);
		}
		else
		{
			uphill = true;
		}
		if (hitInfo2.distance < hitInfo.distance)
		{
			uphill = true;
			downhill = false;
		}
		else if (hitInfo2.distance > hitInfo.distance)
		{
			downhill = true;
			uphill = false;
		}
		else if (hitInfo2.distance == hitInfo.distance)
		{
			flatSurface = true;
			uphill = false;
			downhill = false;
		}
	}
}
public class SlopeDetectionFix : MonoBehaviour
{
	[SerializeField]
	private GameObject cacouc;

	[SerializeField]
	private float dir = 1f;

	private void Start()
	{
	}

	private void Update()
	{
		base.transform.localPosition = cacouc.transform.localPosition + cacouc.transform.forward * dir;
	}
}
public class Stick : MonoBehaviour
{
	[SerializeField]
	private float impactPower;

	[SerializeField]
	private AudioClip impactClip;

	public float mouseX;

	public float mouseY;

	private GameObject player;

	private void Awake()
	{
		player = GameObject.Find("Player");
	}

	private void Update()
	{
		mouseX = Input.GetAxis("Mouse X");
		mouseY = Input.GetAxis("Mouse Y");
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.collider.tag != "Player" && GetComponent<InteractObject>().inHands && (Input.GetButton("Fire1") || Input.GetAxis("LeftClick") > 0.1f))
		{
			SoundManager.Instance.PlaySound(impactClip);
			ContactPoint contactPoint = collision.contacts[0];
			player.GetComponent<FirstPersonController>().AddAccumulatedForce(contactPoint.normal, impactPower * Mathf.Clamp(new Vector2(mouseX, mouseY).magnitude, 0f, 2f));
		}
	}
}
public class TriggerZone : MonoBehaviour
{
	public GameObject sun;

	public void Awake()
	{
		sun = GameObject.Find("Sun");
	}

	private void OnTriggerStay(Collider collisionInfo)
	{
		if (collisionInfo.tag == "Zone")
		{
			RenderSettings.fogColor = UnityEngine.Color.Lerp(RenderSettings.fogColor, collisionInfo.GetComponent<ZoneProperties>().fogColor, 0.01f);
			RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, collisionInfo.GetComponent<ZoneProperties>().fogDensity, 0.01f);
			sun.GetComponent<Light>().color = UnityEngine.Color.Lerp(sun.GetComponent<Light>().color, collisionInfo.GetComponent<ZoneProperties>().sunColor, 0.01f);
			RenderSettings.skybox = collisionInfo.GetComponent<ZoneProperties>().sunSource;
			RenderSettings.ambientIntensity = Mathf.Lerp(RenderSettings.ambientIntensity, collisionInfo.GetComponent<ZoneProperties>().intensityMultiplier, 0.01f);
		}
	}
}
public class Trumpet : MonoBehaviour
{
	private bool active;

	private AudioSource audio;

	public int scaleIndex;

	public float[] scaleFactor;

	public float[] scaleDecal;

	public float[] scaleLimit;

	[SerializeField]
	private GameObject scalePopup;

	[SerializeField]
	private string[] scaleName;

	private void Awake()
	{
		audio = GetComponent<AudioSource>();
	}

	private void Start()
	{
		scalePopup = GameObject.Find("ScalePopup");
	}

	private void Update()
	{
		if (!GetComponent<InteractObject>().inHands || GameObject.Find("Player").GetComponent<FirstPersonController>().pause)
		{
			return;
		}
		if (Input.GetButtonDown("Fire1"))
		{
			audio.Play();
			active = true;
		}
		if (Input.GetButtonUp("Fire1") || Input.GetButtonDown("Interact") || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
		{
			audio.Stop();
			active = false;
		}
		audio.pitch = Mathf.Round((0f - GameObject.Find("Player").GetComponent<FirstPersonController>().rotationX) / 90f * scaleLimit[scaleIndex] * scaleFactor[scaleIndex]) / scaleFactor[scaleIndex] + scaleDecal[scaleIndex];
		if (Input.GetAxis("Mouse ScrollWheel") > 0f || Input.GetButtonDown("Slide"))
		{
			if (scaleIndex >= scaleFactor.Length - 1)
			{
				scaleIndex = 0;
			}
			else
			{
				scaleIndex++;
			}
			scalePopup.GetComponent<ScalePopup>().textLabel.text = scaleName[scaleIndex];
			scalePopup.GetComponent<ScalePopup>().c.a = 2f;
			scalePopup.GetComponent<ScalePopup>().c1.a = 1f;
			scalePopup.GetComponent<ScalePopup>().timer = 0.7f;
		}
		if (Input.GetAxis("Mouse ScrollWheel") < 0f || Input.GetButtonDown("Slide"))
		{
			if (scaleIndex <= 0)
			{
				scaleIndex = scaleFactor.Length - 1;
			}
			else
			{
				scaleIndex--;
			}
			scalePopup.GetComponent<ScalePopup>().textLabel.text = scaleName[scaleIndex];
			scalePopup.GetComponent<ScalePopup>().c.a = 2f;
			scalePopup.GetComponent<ScalePopup>().c1.a = 1f;
			scalePopup.GetComponent<ScalePopup>().timer = 0.7f;
		}
	}
}
public class WeaponSwitching : MonoBehaviour
{
	public int selectedWeapon;

	public AudioClip[] switchingClip;

	private void Start()
	{
		SelectWeapon();
	}

	private void Update()
	{
		int num = selectedWeapon;
		if (Input.GetAxis("Mouse ScrollWheel") > 0f)
		{
			if (selectedWeapon >= base.transform.childCount - 1)
			{
				selectedWeapon = 0;
			}
			else
			{
				selectedWeapon++;
			}
		}
		if (Input.GetAxis("Mouse ScrollWheel") < 0f)
		{
			if (selectedWeapon <= 0)
			{
				selectedWeapon = base.transform.childCount - 1;
			}
			else
			{
				selectedWeapon--;
			}
		}
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			selectedWeapon = 0;
		}
		if (Input.GetKeyDown(KeyCode.Alpha2) && base.transform.childCount >= 2)
		{
			selectedWeapon = 1;
		}
		if (Input.GetKeyDown(KeyCode.Alpha3) && base.transform.childCount >= 3)
		{
			selectedWeapon = 2;
		}
		if (num != selectedWeapon)
		{
			SelectWeapon();
		}
	}

	private void SelectWeapon()
	{
		int num = 0;
		foreach (Transform item in base.transform)
		{
			if (num == selectedWeapon)
			{
				item.gameObject.SetActive(value: true);
			}
			else
			{
				item.gameObject.SetActive(value: false);
			}
			num++;
		}
		SoundManager.Instance.PlaySound(switchingClip[selectedWeapon]);
	}
}
public class ZoneProperties : MonoBehaviour
{
	public UnityEngine.Color fogColor;

	public UnityEngine.Color sunColor;

	public float fogDensity;

	public float intensityMultiplier;

	public Material sunSource;
}
public class ControllerControlsPanel : MonoBehaviour
{
	[SerializeField]
	private GameObject panel;

	private void Start()
	{
	}

	private void Update()
	{
		if (Input.GetJoystickNames().Length != 0)
		{
			if (Input.GetJoystickNames()[0] != "")
			{
				panel.SetActive(value: true);
			}
			else
			{
				panel.SetActive(value: false);
			}
		}
	}
}
public class DestructibleObject : MonoBehaviour
{
	private Rigidbody r;

	private GameObject player;

	private bool separate;

	private void Awake()
	{
		player = GameObject.Find("Player");
		r = GetComponent<Rigidbody>();
	}

	private void Start()
	{
	}

	private void Update()
	{
		if (base.transform.parent == null && separate)
		{
			separate = false;
			r.AddForce(player.GetComponent<FirstPersonController>().playerCamera.transform.forward * 60f, ForceMode.Impulse);
		}
	}

	private void OnTriggerStay(Collider collisionInfo)
	{
		if (!(base.transform.parent != null))
		{
			return;
		}
		if (base.transform.parent.GetComponent<PlankParent>().destroyed)
		{
			if ((float)UnityEngine.Random.Range(0, 10) < 1f)
			{
				SoundManager.Instance.PlaySound(GameObject.Find("GameManager").GetComponent<GameManager>().destroy);
			}
			base.transform.parent.GetComponent<PlankParent>().destroyed = true;
			base.transform.parent = null;
			base.gameObject.layer = 12;
			r.constraints = RigidbodyConstraints.None;
			r.AddForce(player.GetComponent<FirstPersonController>().playerCamera.transform.forward * 60f, ForceMode.Impulse);
			separate = true;
		}
		else if ((collisionInfo.gameObject.layer == 16 || collisionInfo.gameObject.layer == 7) && Input.GetButton("Fire1"))
		{
			base.transform.parent.GetComponent<PlankParent>().destroyed = true;
			base.transform.parent = null;
			base.gameObject.layer = 12;
			r.constraints = RigidbodyConstraints.None;
			r.AddForce(player.GetComponent<FirstPersonController>().playerCamera.transform.forward * 60f, ForceMode.Impulse);
			separate = true;
		}
		else if (collisionInfo.tag == "Player" && collisionInfo.GetComponent<FirstPersonController>().slideTimer > 0f && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftAlt) || Input.GetButton("Slide")))
		{
			base.transform.parent.GetComponent<PlankParent>().destroyed = true;
			base.transform.parent = null;
			base.gameObject.layer = 12;
			r.constraints = RigidbodyConstraints.None;
			r.AddForce(player.GetComponent<FirstPersonController>().playerCamera.transform.forward * 60f, ForceMode.Impulse);
			separate = true;
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.collider.GetComponent<Rigidbody>() != null && (collision.collider.gameObject.layer == 16 || collision.collider.gameObject.layer == 7))
		{
			base.transform.parent = null;
			base.gameObject.layer = 12;
			r.constraints = RigidbodyConstraints.None;
			r.AddForce(player.GetComponent<FirstPersonController>().playerCamera.transform.forward * 10f, ForceMode.Impulse);
		}
	}
}
[Serializable]
[CreateAssetMenu(menuName = "Dialogue/DialogueObject")]
public class DialogueObject : ScriptableObject
{
	[SerializeField]
	[TextArea]
	private string[] dialogue;

	public string[] Dialogue => dialogue;
}
public class DialogueUI : MonoBehaviour
{
	public new string name;

	public int NPCid;

	public bool speaking;

	public AudioClip[] voiceClip;

	[SerializeField]
	public GameObject dialogueBox;

	[SerializeField]
	public TMP_Text textLabel;

	[SerializeField]
	public TMP_Text name_label;

	[SerializeField]
	public DialogueObject currentDialogue;

	public GameObject continueDialogueFX;

	private TypewriterEffect typewriterEffect;

	private GameManager gameManager;

	private bool activ = true;

	private void Awake()
	{
		continueDialogueFX = GameObject.Find("continueDialogueFX");
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Start()
	{
		typewriterEffect = GetComponent<TypewriterEffect>();
		CloseDialogueBox();
	}

	public void ShowDialogue(DialogueObject dialogueObject)
	{
		speaking = true;
		dialogueBox.SetActive(value: true);
		continueDialogueFX.SetActive(value: false);
		if (NPCid == 98 && gameManager.secondPart == 1)
		{
			GetComponent<Dragoyevic>().compassOK = true;
		}
		if (GetComponent<Dragoyevic>() != null)
		{
			GetComponent<Dragoyevic>().giveCompass = true;
		}
		name_label.text = name;
		SoundManager.Instance.PlaySound(voiceClip[UnityEngine.Random.Range(0, voiceClip.Length - 1)]);
		StartCoroutine(StepThroughDialogue(dialogueObject));
	}

	private IEnumerator StepThroughDialogue(DialogueObject dialogueObject)
	{
		string[] dialogue2 = dialogueObject.Dialogue;
		foreach (string dialogue in dialogue2)
		{
			yield return RunTypingEffect(dialogue);
			textLabel.text = dialogue;
			yield return null;
			continueDialogueFX.SetActive(value: true);
			yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Fire1") || Input.GetButtonDown("Interact"));
			continueDialogueFX.SetActive(value: false);
		}
		if (GetComponent<Dragoyevic>() != null && activ && gameManager.secondPart == 1)
		{
			activ = false;
			GetComponent<Dragoyevic>().activateBridge = true;
			gameManager.bridgeAudio.Play();
		}
		CloseDialogueBox();
	}

	private IEnumerator RunTypingEffect(string dialogue)
	{
		typewriterEffect.Run(dialogue, textLabel);
		while (typewriterEffect.IsRunning)
		{
			yield return null;
			if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Fire1") || Input.GetButtonDown("Interact"))
			{
				continueDialogueFX.SetActive(value: true);
				typewriterEffect.Stop();
			}
		}
	}

	private void CloseDialogueBox()
	{
		speaking = false;
		dialogueBox.SetActive(value: false);
		textLabel.text = string.Empty;
		if (!GetComponent<NPC>().dialogueDone && ((GetComponent<NPC>().dialogue.Length - 1 == 0) ? (!GetComponent<NPC>().interactedWith) : (GetComponent<NPC>().dialogueIndex > 0)) && (!GetComponent<NPC>().hasSecondPhase || gameManager.secondPart == 0) && gameManager.startTimer < -1f)
		{
			SoundManager.Instance.PlaySound(GetComponent<NPC>().exclamation.GetComponent<ExclamationPoint>().initClip);
		}
	}
}
public class TypewriterEffect : MonoBehaviour
{
	[SerializeField]
	private float typewriterSpeed = 50f;

	private readonly Dictionary<HashSet<char>, float> punctuations = new Dictionary<HashSet<char>, float>
	{
		{
			new HashSet<char> { '.', '!', '?' },
			0.3f
		},
		{
			new HashSet<char> { ',', ';', ':' },
			0.1f
		}
	};

	private Coroutine typingCoroutine;

	public bool IsRunning { get; private set; }

	public void Run(string textToType, TMP_Text textLabel)
	{
		typingCoroutine = StartCoroutine(TypeText(textToType, textLabel));
	}

	public void Stop()
	{
		StopCoroutine(typingCoroutine);
		IsRunning = false;
	}

	private IEnumerator TypeText(string textToType, TMP_Text textLabel)
	{
		IsRunning = true;
		textLabel.text = string.Empty;
		float t = 0f;
		int charIndex2 = 0;
		while (charIndex2 < textToType.Length)
		{
			int num = charIndex2;
			t += Time.deltaTime * typewriterSpeed;
			charIndex2 = Mathf.FloorToInt(t);
			charIndex2 = Mathf.Clamp(charIndex2, 0, textToType.Length);
			for (int i = num; i < charIndex2; i++)
			{
				bool flag = i >= textToType.Length - 1;
				textLabel.text = textToType.Substring(0, i + 1);
				if (IsPunctuation(textToType[i], out var waitTime) && !flag && !IsPunctuation(textToType[i + 1], out var _))
				{
					yield return new WaitForSeconds(waitTime);
				}
			}
			yield return null;
		}
		IsRunning = false;
	}

	private bool IsPunctuation(char character, out float waitTime)
	{
		foreach (KeyValuePair<HashSet<char>, float> punctuation in punctuations)
		{
			if (punctuation.Key.Contains(character))
			{
				waitTime = punctuation.Value;
				return true;
			}
		}
		waitTime = 0f;
		return false;
	}
}
public class Dragoyevic : MonoBehaviour
{
	public bool giveCompass;

	public bool compassAvailable;

	public bool activateBridge;

	public bool compassOK;

	private bool compassGive;

	private GameManager gameManager;

	private PickupItem player;

	private void Start()
	{
		compassAvailable = true;
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
		player = GameObject.Find("Player").GetComponent<PickupItem>();
	}

	private void Update()
	{
		if (giveCompass && GetComponent<NPC>().dialogueIndex2 == 0 && compassAvailable && compassOK)
		{
			giveCompass = false;
			compassAvailable = false;
			UnityEngine.Debug.Log("cacou");
			player.GetCompass();
		}
		if (activateBridge)
		{
			gameManager.bridgeTimer = 5f;
			activateBridge = false;
		}
		if (gameManager.bridgeTimer > 0f)
		{
			ActivateBridge();
		}
		if (gameManager.bridgeTimer < 1f && gameManager.bridgeTimer > 0f)
		{
			gameManager.bridgeCam.SetActive(value: false);
			gameManager.userInterface.SetActive(value: true);
			gameManager.userInterface1.SetActive(value: true);
		}
	}

	private void ActivateBridge()
	{
		gameManager.bridgeCam.SetActive(value: true);
		gameManager.userInterface.SetActive(value: false);
		gameManager.userInterface1.SetActive(value: false);
		gameManager.anim1.SetBool("Trig1", value: true);
		gameManager.anim2.SetBool("Trig1", value: true);
	}
}
public class AmbientVoiceEmitter : MonoBehaviour
{
	private AudioSource audio;

	[SerializeField]
	private AudioClip sound;

	[SerializeField]
	private float playFrequency;

	public float timer;

	public bool active;

	private void Awake()
	{
		audio = GetComponent<AudioSource>();
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (timer < 0f && active)
		{
			timer = playFrequency;
			audio.PlayOneShot(sound);
		}
		if (GetComponent<DialogueUI>() != null)
		{
			if (GetComponent<DialogueUI>().speaking)
			{
				audio.mute = true;
			}
			else
			{
				audio.mute = false;
			}
		}
	}
}
public class Asker : MonoBehaviour
{
	[SerializeField]
	public DialogueObject secondDialogue;

	[SerializeField]
	private GameObject ticket;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (gameManager.secondPart == 1 && gameManager.haveTicket == 0)
		{
			GetComponent<AmbientVoiceEmitter>().active = true;
			GetComponent<NPC>().dialogue[0] = secondDialogue;
		}
		else
		{
			GetComponent<AmbientVoiceEmitter>().active = false;
		}
		if (GetComponent<NPC>().dialogue[0] == secondDialogue && GetComponent<DialogueUI>().speaking && ticket != null && gameManager.haveTicket == 0 && !gameManager.endGame)
		{
			ticket.SetActive(value: true);
		}
	}
}
public class EndGameTrain : MonoBehaviour
{
	[SerializeField]
	private Transform endPos;

	[SerializeField]
	private float trainSpeed;

	[SerializeField]
	private float accel;

	private GameManager gameManager;

	private AudioSource audio;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
		audio = GameObject.Find("AmbientSource").GetComponent<AudioSource>();
	}

	private void Update()
	{
		if (gameManager.endGame)
		{
			GetComponent<AudioSource>().volume = Mathf.Lerp(GetComponent<AudioSource>().volume, 0.1f, 1f * Time.deltaTime);
			audio.mute = true;
			base.transform.position = new Vector3(base.transform.position.x - 10f * Time.deltaTime, base.transform.position.y, base.transform.position.z);
		}
		else if (gameManager.requestTrain == 1)
		{
			base.transform.position = Vector3.Lerp(base.transform.position, endPos.transform.position, trainSpeed * Time.deltaTime);
		}
	}
}
public class EventZone : MonoBehaviour
{
	[SerializeField]
	private bool oneTimeEvent = true;

	private bool done;

	[SerializeField]
	private AudioSource[] audioSource;

	[SerializeField]
	private Animator[] anim;

	private void Start()
	{
	}

	private void Update()
	{
	}

	private void OnTriggerEnter(Collider collision)
	{
		if (collision.tag == "Player" && oneTimeEvent && !done)
		{
			for (int i = 0; i < audioSource.Length; i++)
			{
				audioSource[i].Play();
			}
			for (int j = 0; j < anim.Length; j++)
			{
				anim[j].SetBool("Bool1", value: true);
			}
			done = true;
		}
	}
}
public class FarShoutZone : MonoBehaviour
{
	[SerializeField]
	private AudioSource shoutLeft;

	[SerializeField]
	private AudioSource shoutRight;

	[SerializeField]
	private float resetTime = 60f;

	private float timer;

	private void Start()
	{
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (timer > resetTime - 1f)
		{
			shoutLeft.Play();
			shoutRight.Play();
		}
		if (timer > resetTime - 30f)
		{
			shoutLeft.loop = true;
			shoutRight.loop = true;
		}
		else
		{
			shoutLeft.loop = false;
			shoutRight.loop = false;
		}
	}

	private void OnTriggerEnter(Collider collision)
	{
		if (collision.tag == "Player" && timer < 0f)
		{
			timer = resetTime;
		}
	}
}
public class GirlDialogue : MonoBehaviour
{
	[SerializeField]
	public DialogueObject secondDialogue;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (gameManager.impressGirl)
		{
			GetComponent<NPC>().dialogue[0] = secondDialogue;
		}
	}
}
public class Jukebox : Interactable
{
	[SerializeField]
	private AudioClip clip;

	public override void OnFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().jukeboxPopup.SetActive(value: true);
	}

	public override void OnInteract()
	{
		SoundManager.Instance.PlaySound(clip);
		GetComponent<AudioSource>().mute = !GetComponent<AudioSource>().mute;
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().jukeboxPopup.SetActive(value: false);
	}
}
public class MecChelouZone : MonoBehaviour
{
	[SerializeField]
	private Animator anim;

	[SerializeField]
	private AudioClip open;

	[SerializeField]
	private AudioClip close;

	private void OnTriggerEnter(Collider collisionInfo)
	{
		if (collisionInfo.tag == "Player")
		{
			anim.SetBool("Bool1", value: true);
			SoundManager.Instance.PlaySound(open);
		}
	}

	private void OnTriggerExit(Collider collisionInfo)
	{
		anim.SetBool("Bool1", value: false);
		SoundManager.Instance.PlaySound(close);
	}
}
public class NoTicketPopup : MonoBehaviour
{
	private float timer = -5f;

	[SerializeField]
	private TMP_Text textLabel;

	private UnityEngine.Color c;

	private GameManager gameManager;

	private void Awake()
	{
		c = textLabel.color;
		c.a = 0f;
	}

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		textLabel.color = c;
		timer -= Time.deltaTime;
		if (gameManager.noticketPopup)
		{
			c.a = 1f;
			timer = 3f;
			gameManager.noticketPopup = false;
		}
		if (timer < 0f && timer > -5f && c.a >= 0f)
		{
			c.a -= 1.5f * Time.deltaTime;
		}
	}
}
public class RequestTrain : Interactable
{
	private Material[] materials;

	public AudioClip train;

	public AudioClip sound;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	public override void OnFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().noticketPopup.SetActive(value: true);
	}

	public override void OnInteract()
	{
		if (gameManager.haveTicket == 1)
		{
			gameManager.haveTicket = 0;
			gameManager.requestTrain = 1;
			SoundManager.Instance.PlaySound(sound);
			SoundManager.Instance.PlaySound(train);
			GetComponent<AudioSource>().Play();
		}
		else
		{
			gameManager.noticketPopup = true;
		}
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().noticketPopup.SetActive(value: false);
	}

	private void Update()
	{
	}
}
public class TrainMovement : MonoBehaviour
{
	[SerializeField]
	private Transform startPos;

	[SerializeField]
	private Transform endPos;

	[SerializeField]
	private float trainSpeed;

	[SerializeField]
	private Vector3 direction;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (!GameObject.Find("Player").GetComponent<FirstPersonController>().pause)
		{
			base.transform.position += Vector3.right * trainSpeed * Time.deltaTime;
		}
		if (((trainSpeed < 0f) ? (base.transform.position.x < endPos.position.x) : (base.transform.position.x > endPos.position.x)) && gameManager.requestTrain == 0)
		{
			base.transform.position = new Vector3(startPos.position.x, base.transform.position.y, base.transform.position.z);
		}
	}
}
public class TravelAgent : MonoBehaviour
{
	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (GetComponent<DialogueUI>().speaking)
		{
			gameManager.secondPart = 1;
		}
	}
}
public class WifeBreathing : MonoBehaviour
{
	private AudioSource audio;

	private GameManager gameManager;

	private void Awake()
	{
		audio = GetComponent<AudioSource>();
	}

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (gameManager.secondPart == 1)
		{
			audio.mute = true;
		}
	}
}
public class ExclamationPoint : MonoBehaviour
{
	public float glowTimer;

	public float glowSpeed;

	public float glowAmount;

	private Vector3 initScale;

	public AudioClip initClip;

	private void Start()
	{
		initScale = base.transform.localScale;
	}

	private void Update()
	{
		glowTimer += Time.deltaTime * glowSpeed;
		base.transform.localScale = initScale * Mathf.Sin(glowTimer) * glowAmount;
	}
}
public class GamepadPopup : MonoBehaviour
{
	private float timer = -5f;

	[SerializeField]
	private TMP_Text textLabel;

	[SerializeField]
	private UnityEngine.UI.Image textLabel1;

	private UnityEngine.Color c;

	private UnityEngine.Color c1;

	private void Awake()
	{
		c = textLabel.color;
		c1 = textLabel1.color;
		c.a = 0f;
		c1.a = 0f;
	}

	private void Update()
	{
		textLabel.color = c;
		textLabel1.color = c1;
		timer -= Time.deltaTime;
		if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Q)) && GameObject.Find("GameManager").GetComponent<GameManager>().gamepad && !GameObject.Find("Player").GetComponent<FirstPersonController>().pause)
		{
			c.a = 1f;
			c1.a = 1f;
			timer = 0.7f;
		}
		if (timer < 0f)
		{
			if (c.a >= 0f)
			{
				c.a -= 2f * Time.deltaTime;
			}
			if (c1.a >= 0f)
			{
				c1.a -= 2f * Time.deltaTime;
			}
		}
	}
}
public class HeadCheckmark : MonoBehaviour
{
	private FirstPersonController player;

	private void Start()
	{
		player = GameObject.Find("Player").GetComponent<FirstPersonController>();
	}

	private void Update()
	{
		player.camController = GetComponent<Toggle>().isOn;
	}
}
public class Door : Interactable
{
	private bool isOpen;

	private bool canBeInteractedWith = true;

	private Animator anim;

	public bool canClose = true;

	public AudioClip openDoorClip;

	private void Start()
	{
		anim = GetComponent<Animator>();
		base.gameObject.layer = 11;
	}

	public override void OnFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().doorPopup.SetActive(value: true);
	}

	public override void OnInteract()
	{
		if (canBeInteractedWith && canClose)
		{
			if (!isOpen)
			{
				canClose = false;
			}
			isOpen = !isOpen;
			Vector3 lhs = base.transform.TransformDirection(Vector3.right);
			Vector3 vector = FirstPersonController.instance.transform.position - base.transform.position;
			float value = Vector3.Dot(lhs, -vector);
			anim.SetFloat("dot", value);
			anim.SetBool("isOpen", isOpen);
			SoundManager.Instance.PlaySound(openDoorClip);
		}
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().doorPopup.SetActive(value: false);
	}

	public void CanClose()
	{
		canClose = true;
	}
}
public abstract class Interactable : MonoBehaviour
{
	public virtual void Awake()
	{
	}

	public abstract void OnInteract();

	public abstract void OnFocus();

	public abstract void OnLoseFocus();
}
public class InteractObject : Interactable
{
	public bool inHands;

	public bool throwObject;

	public bool extended;

	public LayerMask defaultMask;

	public LayerMask inHandsMask;

	private Material[] materials;

	public Rigidbody rb;

	private float defaultYPos;

	public AudioClip interactionClip;

	public Collider collider;

	public GameObject secret;

	private float timer;

	private float climbTimer;

	private bool club;

	private bool climber;

	private bool tinyobject;

	private bool stick;

	private bool moto;

	private bool trumpet;

	private GameObject player;

	public bool onMoto;

	[Header("Position")]
	public float amount = 0.02f;

	public float maxAmount = 0.06f;

	public float smoothAmount = 6f;

	[Header("Rotation")]
	public float rotationAmount = 4f;

	public float maxRotationAmount = 5f;

	public float smoothRotation = 12f;

	[Space]
	public bool rotationX = true;

	public bool rotationY = true;

	public bool rotationZ = true;

	private float InputX;

	private float InputY;

	private float movTimer;

	public override void OnFocus()
	{
		Material[] array = materials;
		foreach (Material material in array)
		{
			if (!inHands)
			{
				material.SetFloat("_OutlineWidth", 0.015f);
			}
		}
		if (!inHands)
		{
			GameObject.Find("Player").GetComponent<FirstPersonController>().grabPopup.SetActive(value: true);
		}
	}

	public override void OnInteract()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().grabPopup.SetActive(value: false);
		if (inHands)
		{
			return;
		}
		SoundManager.Instance.PlaySound(interactionClip);
		if (GetComponent<ItemProperties>().id == 0 || GetComponent<ItemProperties>().id == 1 || GetComponent<ItemProperties>().id == 2 || GetComponent<ItemProperties>().id == 4 || GetComponent<ItemProperties>().id == 5 || GetComponent<ItemProperties>().id == 6 || GetComponent<ItemProperties>().id == 7 || GetComponent<ItemProperties>().id == 8 || GetComponent<ItemProperties>().id == 9 || GetComponent<ItemProperties>().id == 10 || GetComponent<ItemProperties>().id == 11 || GetComponent<ItemProperties>().id == 13 || GetComponent<ItemProperties>().id == 16 || (GetComponent<ItemProperties>().id == 12 && GameObject.Find("GameManager").GetComponent<GameManager>().haveTicket == 1))
		{
			base.transform.position = GameObject.Find("ObjectPos").transform.position;
			inHands = !inHands;
			if (GetComponent<ItemProperties>().id == 10)
			{
				UnityEngine.Object.Destroy(GameObject.Find("Arrow"));
			}
			throwObject = true;
			timer = 0.3f;
			GameObject.Find("GameManager").GetComponent<GameManager>().pickup = true;
			GameObject.Find("GameManager").GetComponent<GameManager>().item = GetComponent<ItemProperties>().id;
			if (base.gameObject.tag == "guidon")
			{
				UnityEngine.Object.Destroy(base.transform.parent.gameObject);
			}
			UnityEngine.Object.Destroy(base.gameObject);
		}
		if (GetComponent<ItemProperties>().id == 3)
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().AddSecret();
			GameObject.Find("GameManager").GetComponent<GameManager>().secretPopup = true;
			UnityEngine.Object.Destroy(base.gameObject);
			GameObject.Find("GameManager").GetComponent<GameManager>().secretState[secret.GetComponent<SecretObjectDisplay>().secretId] = 1;
		}
		if (GetComponent<ItemProperties>().id == 50)
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().haveTicket = 1;
			if (!GameObject.Find("GameManager").GetComponent<GameManager>().ticket)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().Popup();
			}
			GameObject.Find("GameManager").GetComponent<GameManager>().ticket = true;
			GameObject.Find("GameManager").GetComponent<GameManager>().ticketState = 1;
			GameObject.Find("GameManager").GetComponent<GameManager>().lastAchievement = "Babbdi quest";
			SteamIntegration.Instance.UnlockAchivement("BabbdiQuest");
			UnityEngine.Object.Destroy(base.gameObject);
		}
		if (GetComponent<ItemProperties>().id == 14)
		{
			GameObject.Find("GameManager").GetComponent<GameManager>().haveMap = 1;
			GameObject.Find("GameManager").GetComponent<GameManager>().mapPopup = true;
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().grabPopup.SetActive(value: false);
	}

	public new void Awake()
	{
		defaultYPos = base.transform.position.y;
		collider = GetComponent<Collider>();
		player = GameObject.Find("Player");
		materials = GetComponent<Renderer>().materials;
		defaultMask = base.gameObject.layer;
	}

	public void Update()
	{
		timer -= Time.deltaTime;
		if (inHands)
		{
			player.GetComponent<FirstPersonController>().inHands = true;
		}
		if (inHands && GetComponent<ItemProperties>().id != 11)
		{
			base.gameObject.layer = 16;
		}
		else
		{
			base.gameObject.layer = 7;
		}
		if (player.GetComponent<FirstPersonController>().currentObject != base.gameObject)
		{
			Material[] array = materials;
			foreach (Material material in array)
			{
				if (!inHands)
				{
					material.SetFloat("_OutlineWidth", 0f);
				}
			}
		}
		if (throwObject)
		{
			player.GetComponent<FirstPersonController>().inHands = false;
			GameObject.Find("Player").GetComponent<FirstPersonController>().grabPopup.SetActive(value: false);
		}
		if (player.GetComponent<FirstPersonController>().pause)
		{
			return;
		}
		if (GetComponent<ItemProperties>().id == 0 || GetComponent<ItemProperties>().id == 7)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().ball = 1;
				collider.isTrigger = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Fire1") || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.isKinematic = true;
				rb.velocity = new Vector3(0f, 0f, 0f);
				tinyobject = true;
				WeaponSway(GameObject.Find("ObjectPos").transform.position);
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().ball = 0;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.isKinematic = false;
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 1)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().club = 1;
				collider.isTrigger = true;
				rb.useGravity = false;
				rb.isKinematic = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				if (Input.GetButton("Fire1"))
				{
					base.transform.position = GameObject.Find("ObjectPos3").transform.position;
					base.transform.rotation = GameObject.Find("ObjectPos3").transform.rotation;
					extended = true;
					club = false;
				}
				else
				{
					club = true;
					ItemMovement(GameObject.Find("ObjectPos2").transform.position, GameObject.Find("ObjectPos2").transform);
					extended = false;
				}
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().club = 0;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.isKinematic = false;
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				club = false;
				throwObject = false;
			}
		}
		climbTimer -= Time.deltaTime;
		if (GetComponent<ItemProperties>().id == 2)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().climber = 1;
				collider.isTrigger = true;
				if (!extended && timer < 0f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && !GetComponent<Climber>().trigger && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.isKinematic = true;
				if (extended && GetComponent<Climber>().trigger)
				{
					base.transform.position = GetComponent<Climber>().pickPos;
					base.transform.rotation = GetComponent<Climber>().pickRot;
					climber = false;
				}
				else if (Input.GetButton("Fire1") && GetComponent<Climber>().pick)
				{
					base.transform.position = GameObject.Find("Pickaxe00").transform.position;
					base.transform.rotation = GameObject.Find("Pickaxe00").transform.rotation;
					extended = true;
					climbTimer = 0.01f;
					climber = false;
				}
				else
				{
					climber = true;
					ItemMovement(GameObject.Find("Pickaxe01").transform.position, GameObject.Find("Pickaxe01").transform);
					extended = false;
				}
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().climber = 0;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.isKinematic = false;
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 4)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().flashlight = 1;
				collider.isTrigger = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && timer < 0f && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.isKinematic = true;
				base.transform.rotation = GameObject.Find("LightPos").transform.rotation;
				base.transform.position = GameObject.Find("LightPos").transform.position;
			}
			else if (throwObject)
			{
				rb.isKinematic = false;
				GameObject.Find("GameManager").GetComponent<GameManager>().flashlight = 0;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 5)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().propeller = 1;
				collider.isTrigger = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && timer < 0f && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.velocity = new Vector3(0f, 0f, 0f);
				base.transform.rotation = GameObject.Find("PropellerPos").transform.rotation;
				base.transform.position = GameObject.Find("PropellerPos").transform.position;
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().propeller = 0;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 1f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 6)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().blower = 1;
				collider.isTrigger = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && timer < 0f && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.velocity = new Vector3(0f, 0f, 0f);
				base.transform.rotation = GameObject.Find("BlowerPos").transform.rotation;
				base.transform.position = GameObject.Find("BlowerPos").transform.position;
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().blower = 0;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 8)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().bigball = 1;
				collider.isTrigger = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && timer < 0f && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.velocity = new Vector3(0f, 0f, 0f);
				GameObject.Find("Player").GetComponent<FirstPersonController>().bigball = true;
				base.transform.rotation = GameObject.Find("BigBallPos").transform.rotation;
				base.transform.position = GameObject.Find("BigBallPos").transform.position;
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().bigball = 0;
				collider.isTrigger = false;
				GameObject.Find("Player").GetComponent<FirstPersonController>().bigball = false;
				base.transform.SetParent(null);
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 9)
		{
			if (inHands)
			{
				collider.isTrigger = false;
				rb.useGravity = false;
				rb.velocity = new Vector3(0f, 0f, 0f);
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				if (Input.GetButton("Fire1") || Input.GetAxis("LeftClick") > 0.1f)
				{
					rb.useGravity = false;
					ItemMovement(GameObject.Find("StickPos00").transform.position, GameObject.Find("StickPos00").transform);
					extended = true;
					stick = false;
				}
				else
				{
					rb.useGravity = false;
					stick = true;
					ItemMovement(GameObject.Find("StickPos01").transform.position, GameObject.Find("StickPos01").transform);
					extended = false;
				}
			}
			else if (throwObject)
			{
				base.transform.SetParent(null);
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				stick = false;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 10)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().grabber = 1;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && timer < 0f && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.velocity = new Vector3(0f, 0f, 0f);
				base.transform.rotation = GameObject.Find("GrabberPos").transform.rotation;
				base.transform.position = GameObject.Find("GrabberPos").transform.position;
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().grabber = 0;
				collider.isTrigger = false;
				GameObject.Find("Player").GetComponent<FirstPersonController>().bigball = false;
				base.transform.SetParent(null);
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 12)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().compass = 1;
				collider.isTrigger = true;
				rb.useGravity = false;
				rb.isKinematic = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && !player.GetComponent<EnterZone>().inLift && (GameObject.Find("GameManager").GetComponent<GameManager>().secondPart == 0 || GameObject.Find("GameManager").GetComponent<GameManager>().haveTicket == 1))
				{
					throwObject = true;
					inHands = false;
				}
				if (Input.GetButton("Fire2") && player.GetComponent<FirstPersonController>().compassTimer < 0f)
				{
					player.GetComponent<FirstPersonController>().initRotationX = player.GetComponent<FirstPersonController>().playerCamera.transform.rotation;
					player.GetComponent<FirstPersonController>().initRotationY = player.transform.eulerAngles.y;
				}
				if (Input.GetButton("Fire2"))
				{
					player.GetComponent<FirstPersonController>().compassLook = true;
				}
				else
				{
					player.GetComponent<FirstPersonController>().compassLook = false;
					rb.useGravity = false;
					stick = true;
					extended = false;
				}
				base.transform.position = GameObject.Find("CompassPos").transform.position;
				base.transform.rotation = GameObject.Find("CompassPos").transform.rotation;
			}
			else if (throwObject)
			{
				player.GetComponent<FirstPersonController>().compassLook = false;
				GameObject.Find("GameManager").GetComponent<GameManager>().compass = 0;
				base.transform.SetParent(null);
				rb.isKinematic = false;
				collider.isTrigger = false;
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				stick = false;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 11)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().motorBike = 1;
				onMoto = true;
				collider.isTrigger = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && timer < 0f && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.velocity = new Vector3(0f, 0f, 0f);
				base.transform.position = new Vector3(GameObject.Find("MotoPos").transform.position.x, player.transform.position.y, GameObject.Find("MotoPos").transform.position.z);
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().motorBike = 0;
				onMoto = false;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 13)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().trumpet = 1;
				trumpet = true;
				collider.isTrigger = true;
				rb.useGravity = false;
				rb.position = GameObject.Find("TrumpetPos00").transform.position;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				if (Input.GetButton("Fire1"))
				{
					rb.useGravity = false;
					ItemMovement(GameObject.Find("TrumpetPos00").transform.position, GameObject.Find("TrumpetPos01").transform);
					extended = true;
				}
				else
				{
					rb.useGravity = false;
					ItemMovement(GameObject.Find("TrumpetPos01").transform.position, GameObject.Find("TrumpetPos01").transform);
					extended = false;
				}
			}
			else if (throwObject)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().trumpet = 0;
				base.transform.SetParent(null);
				rb.isKinematic = false;
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 10f, ForceMode.Impulse);
				rb.useGravity = true;
				trumpet = false;
				collider.isTrigger = false;
				throwObject = false;
			}
		}
		if (GetComponent<ItemProperties>().id == 16)
		{
			if (inHands)
			{
				GameObject.Find("GameManager").GetComponent<GameManager>().secretfinder = 1;
				collider.isTrigger = true;
				if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetButtonDown("Interact")) && player.GetComponent<FirstPersonController>().canThrow && timer < 0f && !player.GetComponent<EnterZone>().inLift)
				{
					throwObject = true;
					inHands = false;
				}
				rb.useGravity = false;
				rb.isKinematic = true;
				base.transform.rotation = GameObject.Find("SecretFinderPos").transform.rotation;
				base.transform.position = GameObject.Find("SecretFinderPos").transform.position;
				if (Input.GetButton("Fire1") && player.GetComponent<FirstPersonController>().secretTimer < 0f && GameObject.Find("GameManager").GetComponent<GameManager>().secretsFound < 21)
				{
					player.GetComponent<FirstPersonController>().initRotationX = player.GetComponent<FirstPersonController>().playerCamera.transform.rotation;
					player.GetComponent<FirstPersonController>().initRotationY = player.transform.eulerAngles.y;
				}
				if (Input.GetButton("Fire1") && GameObject.Find("GameManager").GetComponent<GameManager>().secretsFound < 21)
				{
					player.GetComponent<FirstPersonController>().secretLook = true;
				}
				else
				{
					player.GetComponent<FirstPersonController>().secretLook = false;
					rb.useGravity = false;
					stick = true;
					extended = false;
				}
			}
			else if (throwObject)
			{
				player.GetComponent<FirstPersonController>().secretLook = false;
				rb.isKinematic = false;
				GameObject.Find("GameManager").GetComponent<GameManager>().secretfinder = 0;
				collider.isTrigger = false;
				base.transform.SetParent(null);
				rb.AddForce(GameObject.Find("Main Camera").transform.forward * 5f, ForceMode.Impulse);
				rb.useGravity = true;
				throwObject = false;
			}
		}
		if (inHands)
		{
			_ = GameObject.Find("Player").GetComponent<FirstPersonController>().landTimer;
			_ = 0f;
		}
	}

	private void FixedUpdate()
	{
		if (inHands)
		{
			if (tinyobject && (GetComponent<ItemProperties>().id == 0 || (tinyobject && GetComponent<ItemProperties>().id == 7)))
			{
				TiltSway(GameObject.Find("ObjectPos").transform.localRotation);
			}
			if (GetComponent<ItemProperties>().id == 1 && club)
			{
				TiltSway(GameObject.Find("ObjectPos2").transform.localRotation);
			}
			if (GetComponent<ItemProperties>().id == 13 && trumpet && extended)
			{
				TiltSway(GameObject.Find("TrumpetPos00").transform.localRotation);
			}
			if (GetComponent<ItemProperties>().id == 13 && trumpet && !extended)
			{
				TiltSway(GameObject.Find("TrumpetPos01").transform.localRotation);
			}
			if (GetComponent<ItemProperties>().id == 2 && climber)
			{
				TiltSway(GameObject.Find("Pickaxe01").transform.localRotation);
			}
			if (GetComponent<ItemProperties>().id == 9 && stick)
			{
				TiltSway(GameObject.Find("StickPos01").transform.localRotation);
			}
			if (GetComponent<ItemProperties>().id == 9 && !stick)
			{
				base.transform.rotation = GameObject.Find("StickPos00").transform.rotation;
			}
			if (GetComponent<ItemProperties>().id == 11 && inHands)
			{
				TiltSway(Quaternion.Euler(0f, GameObject.Find("MotoPos").transform.localRotation.y - 90f, 0f));
			}
		}
	}

	public void WeaponSway(Vector3 initialPosition)
	{
		if (GameObject.Find("GameManager").GetComponent<GameManager>().gamepad)
		{
			InputX = 0f - Input.GetAxis("CameraHorizontal");
			InputY = 0f - Input.GetAxis("CameraVertical");
			float x = Mathf.Clamp(InputX * amount, 0f - maxAmount, maxAmount);
			float y = Mathf.Clamp(InputY * amount, 0f - maxAmount, maxAmount);
			new Vector3(x, y, 0f);
			base.transform.position = Vector3.Lerp(rb.position, initialPosition, Time.deltaTime * smoothAmount);
			rb.position = Vector3.Lerp(rb.position, initialPosition, Time.fixedDeltaTime * smoothAmount);
		}
		else
		{
			InputX = 0f - Input.GetAxis("Mouse X");
			InputY = 0f - Input.GetAxis("Mouse Y");
			float x2 = Mathf.Clamp(InputX * amount, 0f - maxAmount, maxAmount);
			float y2 = Mathf.Clamp(InputY * amount, 0f - maxAmount, maxAmount);
			new Vector3(x2, y2, 0f);
			base.transform.position = Vector3.Lerp(rb.position, initialPosition, Time.deltaTime * smoothAmount);
			rb.position = Vector3.Lerp(rb.position, initialPosition, Time.fixedDeltaTime * smoothAmount);
		}
	}

	public void ItemMovement(Vector3 initialPosition, Transform pos)
	{
		movTimer -= Time.deltaTime;
		float y = initialPosition.y;
		if (player.GetComponent<FirstPersonController>().currentInputRaw != Vector2.zero)
		{
			base.transform.position = new Vector3(initialPosition.x, y + Mathf.Sin(player.GetComponent<FirstPersonController>().timer) * (player.GetComponent<FirstPersonController>().isCrouching ? player.GetComponent<FirstPersonController>().crouchBobAmount : (player.GetComponent<FirstPersonController>().IsSprinting ? (player.GetComponent<FirstPersonController>().sprintBobAmount / 3f) : (player.GetComponent<FirstPersonController>().walkBobAmount / 2f))), initialPosition.z);
			return;
		}
		base.transform.position = initialPosition;
		rb.position = initialPosition;
	}

	public void TiltSway(Quaternion initialRotation)
	{
		if (GameObject.Find("GameManager").GetComponent<GameManager>().gamepad)
		{
			float num = Mathf.Clamp((0f - Input.GetAxis("CameraHorizontal")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			float num2 = Mathf.Clamp((0f - Input.GetAxis("CameraVertical")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			Quaternion quaternion = Quaternion.Euler(new Vector3(rotationX ? num2 : 0f, rotationY ? num : 0f, rotationZ ? num : 0f));
			base.transform.localRotation = Quaternion.Slerp(base.transform.localRotation, quaternion * initialRotation, Time.deltaTime * smoothRotation);
		}
		else
		{
			float num3 = Mathf.Clamp((0f - Input.GetAxis("Mouse X")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			float num4 = Mathf.Clamp((0f - Input.GetAxis("Mouse Y")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
			Quaternion quaternion2 = Quaternion.Euler(new Vector3(rotationX ? num4 : 0f, rotationY ? num3 : 0f, rotationZ ? num3 : 0f));
			base.transform.localRotation = Quaternion.Slerp(base.transform.localRotation, quaternion2 * initialRotation, Time.deltaTime * smoothRotation);
		}
	}

	public void TiltSwayGlobal(Quaternion initialRotation)
	{
		float num = Mathf.Clamp((0f - Input.GetAxis("Mouse X")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
		float num2 = Mathf.Clamp((0f - Input.GetAxis("Mouse Y")) * rotationAmount, 0f - maxRotationAmount, maxRotationAmount);
		Quaternion quaternion = Quaternion.Euler(new Vector3(rotationX ? num2 : 0f, rotationY ? num : 0f, rotationZ ? num : 0f));
		base.transform.rotation = Quaternion.Slerp(base.transform.rotation, quaternion * initialRotation, Time.deltaTime * smoothRotation);
	}
}
public class ItemProperties : MonoBehaviour
{
	public int id;

	public new string name;

	public int value;
}
public class Lift : MonoBehaviour
{
	public GameObject firstPoint;

	public GameObject secondPoint;

	public bool isTriggered;

	public bool stop;

	public float speed = 0.01f;

	public bool topReach;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (isTriggered && !topReach)
		{
			gameManager.inActivatedLift = true;
			base.transform.Translate(Vector3.up * speed * Time.deltaTime);
			stop = true;
		}
		else if (isTriggered && topReach)
		{
			gameManager.inActivatedLift = true;
			base.transform.Translate(Vector3.down * speed * Time.deltaTime);
			stop = true;
		}
		if ((isTriggered && base.transform.position.y > secondPoint.transform.position.y && stop) || (isTriggered && base.transform.position.y < firstPoint.transform.position.y && stop))
		{
			gameManager.inActivatedLift = false;
			topReach = !topReach;
			isTriggered = false;
			stop = false;
		}
	}
}
public class LiftCaller : Interactable
{
	public GameObject objectToActivate;

	public AudioClip activateClip;

	public override void OnFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().elevatorPopup.SetActive(value: true);
	}

	public override void OnInteract()
	{
		if (objectToActivate != null && objectToActivate.GetComponent<Lift>() != null && !objectToActivate.GetComponent<Lift>().isTriggered)
		{
			objectToActivate.GetComponent<Lift>().isTriggered = true;
			SoundManager.Instance.PlaySound(activateClip);
		}
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().elevatorPopup.SetActive(value: false);
	}
}
public class NPC : Interactable
{
	public int npcId;

	public bool hasSecondPhase;

	public bool interactedWith;

	public bool dialogueDone;

	[SerializeField]
	public GameObject exclamation;

	public DialogueObject[] dialogue;

	public DialogueObject[] dialogue2;

	public int dialogueIndex;

	public int dialogueIndex2;

	public int secondPhaseDialogues;

	public int dialogue2Number = 2;

	public override void OnFocus()
	{
		if (!GameObject.Find("Player").GetComponent<FirstPersonController>().dialogueActive)
		{
			GameObject.Find("Player").GetComponent<FirstPersonController>().talkPopup.SetActive(value: true);
		}
	}

	public override void OnInteract()
	{
		dialogueIndex = Mathf.Clamp(dialogueIndex, 0, dialogue.Length - 1);
		dialogueIndex2 = Mathf.Clamp(dialogueIndex2, 0, dialogue2.Length - 1);
		if (GameObject.Find("GameManager").GetComponent<GameManager>().secondPart == 1 && hasSecondPhase && !GameObject.Find("Player").GetComponent<FirstPersonController>().dialogueActive)
		{
			dialogueDone = false;
			GetComponent<DialogueUI>().ShowDialogue(dialogue2[dialogueIndex2]);
			if (dialogueIndex2 >= dialogue2.Length - 1)
			{
				dialogueIndex2 = 0;
			}
			else
			{
				dialogueIndex2++;
			}
			secondPhaseDialogues++;
		}
		else if (!GameObject.Find("Player").GetComponent<FirstPersonController>().dialogueActive)
		{
			GetComponent<DialogueUI>().ShowDialogue(dialogue[dialogueIndex]);
			if (dialogueIndex >= dialogue.Length - 1)
			{
				dialogueIndex = 0;
				dialogueDone = true;
			}
			else
			{
				dialogueIndex++;
			}
		}
		if (!interactedWith)
		{
			interactedWith = true;
			GameObject.Find("GameManager").GetComponent<GameManager>().npcInteractedWith++;
		}
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().talkPopup.SetActive(value: false);
	}

	private void Start()
	{
		dialogue2Number = dialogue2.Length;
	}

	private void Update()
	{
		if (exclamation != null)
		{
			if (!GameObject.Find("Player").GetComponent<FirstPersonController>().dialogueActive && !GetComponent<DialogueUI>().speaking && ((dialogue.Length - 1 == 0) ? (!interactedWith) : (dialogueIndex > 0)) && (!hasSecondPhase || GameObject.Find("GameManager").GetComponent<GameManager>().secondPart == 0) && !dialogueDone)
			{
				exclamation.SetActive(value: true);
			}
			else if (!GameObject.Find("Player").GetComponent<FirstPersonController>().dialogueActive && GameObject.Find("GameManager").GetComponent<GameManager>().secondPart == 1 && hasSecondPhase && secondPhaseDialogues < dialogue2Number)
			{
				exclamation.SetActive(value: true);
			}
			else
			{
				exclamation.SetActive(value: false);
			}
		}
	}
}
public class SystemActivator : Interactable
{
	public GameObject objectToActivate;

	public AudioClip activateClip;

	[SerializeField]
	private bool upCall;

	[SerializeField]
	private bool downCall;

	public override void OnFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().elevatorPopup.SetActive(value: true);
	}

	public override void OnInteract()
	{
		if (objectToActivate != null)
		{
			if (objectToActivate.GetComponent<Lift>() != null && !objectToActivate.GetComponent<Lift>().isTriggered && upCall && !objectToActivate.GetComponent<Lift>().topReach)
			{
				objectToActivate.GetComponent<Lift>().isTriggered = true;
				SoundManager.Instance.PlaySound(activateClip);
			}
			else if (objectToActivate.GetComponent<Lift>() != null && !objectToActivate.GetComponent<Lift>().isTriggered && downCall && objectToActivate.GetComponent<Lift>().topReach)
			{
				objectToActivate.GetComponent<Lift>().isTriggered = true;
				SoundManager.Instance.PlaySound(activateClip);
			}
			if (objectToActivate.GetComponent<Lift>() != null && !objectToActivate.GetComponent<Lift>().isTriggered && !upCall && !downCall)
			{
				objectToActivate.GetComponent<Lift>().isTriggered = true;
				SoundManager.Instance.PlaySound(activateClip);
			}
		}
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().elevatorPopup.SetActive(value: false);
	}
}
public class InvertMouseX : MonoBehaviour
{
	private FirstPersonController player;

	private SavingOptions save;

	private void Start()
	{
		player = GameObject.Find("Player").GetComponent<FirstPersonController>();
		save = GameObject.Find("SavingOptions").GetComponent<SavingOptions>();
		if (save.mouseInvertedX == 1)
		{
			GetComponent<Toggle>().isOn = true;
			player.invertX = true;
		}
		else
		{
			GetComponent<Toggle>().isOn = false;
			player.invertX = false;
		}
	}

	private void Update()
	{
		player.invertX = GetComponent<Toggle>().isOn;
		if (player.invertX)
		{
			save.mouseInvertedX = 1;
		}
		else
		{
			save.mouseInvertedX = 0;
		}
	}
}
public class InvertMouseY : MonoBehaviour
{
	private FirstPersonController player;

	private SavingOptions save;

	private void Start()
	{
		player = GameObject.Find("Player").GetComponent<FirstPersonController>();
		save = GameObject.Find("SavingOptions").GetComponent<SavingOptions>();
		if (save.mouseInvertedY == 1)
		{
			GetComponent<Toggle>().isOn = true;
			player.invertY = true;
		}
		else
		{
			GetComponent<Toggle>().isOn = false;
			player.invertY = false;
		}
	}

	private void Update()
	{
		player.invertY = GetComponent<Toggle>().isOn;
		if (player.invertY)
		{
			save.mouseInvertedY = 1;
		}
		else
		{
			save.mouseInvertedY = 0;
		}
	}
}
public class MainMenuEffect : MonoBehaviour
{
	private float timer = -5f;

	[SerializeField]
	private TMP_Text textLabel;

	public TextMeshProUGUI tmp;

	public float glowTimer;

	public float glowSpeed;

	public float glowAmount;

	private UnityEngine.Color c;

	private void Awake()
	{
		c = textLabel.color;
	}

	private void Update()
	{
		textLabel.color = c;
		glowTimer += Time.deltaTime * glowSpeed;
		timer -= Time.deltaTime;
		tmp.fontMaterial.SetFloat(ShaderUtilities.ID_LightAngle, 3f + Mathf.Sin(glowTimer) * glowAmount);
	}
}
public class MapButton : MonoBehaviour
{
	public bool mouseOver;

	private void Start()
	{
	}

	private void Update()
	{
	}

	public void SetBool(bool p)
	{
		mouseOver = p;
	}
}
public class MapPopup : MonoBehaviour
{
	private float timer = -5f;

	[SerializeField]
	private TMP_Text textLabel;

	[SerializeField]
	private TMP_Text textLabel1;

	private UnityEngine.Color c;

	private UnityEngine.Color c1;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Awake()
	{
		c = textLabel.color;
		c1 = textLabel1.color;
		c.a = 0f;
		c1.a = 0f;
	}

	private void Update()
	{
		textLabel.color = c;
		textLabel1.color = c1;
		timer -= Time.deltaTime;
		if (gameManager.mapPopup)
		{
			c.a = 1f;
			c1.a = 1f;
			timer = 3f;
			gameManager.mapPopup = false;
		}
		if (timer < 0f)
		{
			if (c.a >= 0f)
			{
				c.a -= 2f * Time.deltaTime;
			}
			if (c1.a >= 0f)
			{
				c1.a -= 2f * Time.deltaTime;
			}
		}
	}
}
public class MenuInput : MonoBehaviour
{
	[SerializeField]
	private AudioClip[] jingle;

	[SerializeField]
	private AudioClip press;

	public void OnPress()
	{
		SoundManager.Instance.PlaySound(jingle[UnityEngine.Random.Range(0, jingle.Length - 1)]);
	}

	public void OnClickDown()
	{
		SoundManager.Instance.PlaySound(press);
	}
}
public class Parameters : MonoBehaviour
{
	public static Parameters Instance;

	public bool speedrun;

	private SavingOptions save;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
		save = GameObject.Find("SavingOptions").GetComponent<SavingOptions>();
	}

	private void Start()
	{
		if (save.speedrun == 1)
		{
			speedrun = true;
		}
		else
		{
			speedrun = false;
		}
	}

	public void ValueChange(Toggle toggle)
	{
		speedrun = toggle.isOn;
		if (speedrun)
		{
			save.speedrun = 1;
		}
		else
		{
			save.speedrun = 0;
		}
	}
}
public class PlankParent : MonoBehaviour
{
	public bool destroyed;

	public bool playSound = true;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (playSound && destroyed)
		{
			SoundManager.Instance.PlaySound(gameManager.destroy);
			playSound = false;
		}
	}
}
public class SaveGameFeedback : MonoBehaviour
{
	[SerializeField]
	private GameObject saveLabel;

	private float timer;

	private void Start()
	{
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (timer < 0f)
		{
			base.gameObject.SetActive(value: false);
		}
	}

	public void ResetTimer()
	{
		timer = 2f;
	}
}
public class SavePopup : MonoBehaviour
{
	private float timer = -5f;

	[SerializeField]
	private TMP_Text textLabel;

	public TextMeshProUGUI tmp;

	public float glowTimer;

	public float glowSpeed;

	public float glowAmount;

	private UnityEngine.Color c;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Awake()
	{
		c = textLabel.color;
		c.a = 0f;
	}

	private void Update()
	{
		textLabel.color = c;
		glowTimer += Time.deltaTime * glowSpeed;
		timer -= Time.deltaTime;
		tmp.fontMaterial.SetFloat(ShaderUtilities.ID_LightAngle, 3f + Mathf.Sin(glowTimer) * glowAmount);
		if (gameManager.savePopup)
		{
			glowTimer = 0f;
			c.a = 1f;
			timer = 3f;
			gameManager.savePopup = false;
		}
		if (timer < 0f && c.a >= 0f)
		{
			c.a -= 1.5f * Time.deltaTime;
		}
	}
}
public class ChangeSceneButton : MonoBehaviour
{
	public void ChangeScene(string sceneName)
	{
		LevelManager.Instance.LoadScene(sceneName);
	}
}
public class LevelManager : MonoBehaviour
{
	public static LevelManager Instance;

	[SerializeField]
	private GameObject _loaderCanvas;

	[SerializeField]
	private UnityEngine.UI.Image _progressBar;

	private float _target;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public async void LoadScene(string sceneName)
	{
		_target = 0f;
		_progressBar.fillAmount = 0f;
		AsyncOperation scene = SceneManager.LoadSceneAsync(sceneName);
		scene.allowSceneActivation = false;
		_loaderCanvas.SetActive(value: true);
		do
		{
			await Task.Delay(100);
			_progressBar.fillAmount = scene.progress;
		}
		while (scene.progress < 0.9f);
		scene.allowSceneActivation = true;
	}

	private void Update()
	{
	}
}
public class Screenshake : MonoBehaviour
{
	public bool start;

	public float smallFallDuration = 1f;

	public float bigFallDuration = 1f;

	[HideInInspector]
	public float duration = 1f;

	public AnimationCurve smallCurve;

	public AnimationCurve bigCurve;

	[HideInInspector]
	public AnimationCurve strengthCurve;

	private float timer;

	private Vector3 startingPos;

	private void Start()
	{
		startingPos = Vector3.zero;
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (start)
		{
			timer = duration;
			start = false;
			StartCoroutine(Shaking());
		}
		if (timer < 0f)
		{
			base.transform.localPosition = Vector3.Lerp(base.transform.localPosition, startingPos, 4f * Time.deltaTime);
		}
	}

	private IEnumerator Shaking()
	{
		float elapsedTime = 0f;
		while (elapsedTime < duration)
		{
			elapsedTime += Time.deltaTime;
			float num = strengthCurve.Evaluate(elapsedTime / duration);
			base.transform.localPosition = startingPos + UnityEngine.Random.insideUnitSphere * num;
			yield return null;
		}
		base.transform.localPosition = startingPos;
	}
}
public class SelectButton : MonoBehaviour
{
	private bool plugged;

	[SerializeField]
	private Button button;

	private void Update()
	{
		if (Input.GetJoystickNames().Length == 0)
		{
			plugged = false;
		}
		if (Input.GetJoystickNames().Length != 0 && !plugged && Input.GetJoystickNames()[0] != "")
		{
			UnityEngine.Debug.Log("allo");
			plugged = true;
			button.Select();
		}
	}
}
public class SmoothMouseCheck : MonoBehaviour
{
	private FirstPersonController player;

	private void Start()
	{
		player = GameObject.Find("Player").GetComponent<FirstPersonController>();
	}

	private void Update()
	{
		player.smoothCam = GetComponent<Toggle>().isOn;
	}
}
public class SubwaySound : MonoBehaviour
{
	private AudioSource audio;

	public GameObject[] lights;

	private GameManager gameManager;

	private AudioSource ambient;

	private void Start()
	{
		audio = GetComponent<AudioSource>();
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
		ambient = GameObject.Find("AmbientSource").GetComponent<AudioSource>();
	}

	private void Update()
	{
		if (gameManager.inSubway)
		{
			audio.volume = Mathf.Lerp(audio.volume, ambient.volume, 1f * Time.deltaTime);
		}
		else if (!gameManager.inSubway)
		{
			audio.volume = Mathf.Lerp(audio.volume, 0f, 2f * Time.deltaTime);
		}
		if (gameManager.inSubway)
		{
			for (int i = 0; i < lights.Length; i++)
			{
				lights[i].SetActive(value: true);
			}
		}
		else if (!gameManager.inSubway)
		{
			for (int j = 0; j < lights.Length; j++)
			{
				lights[j].SetActive(value: false);
			}
		}
	}
}
public class AmbientSoundSlider : MonoBehaviour
{
	[SerializeField]
	private Slider _slider;

	private void Awake()
	{
		_slider.value = SoundManager.Instance._ambientSource.volume;
	}

	private void Start()
	{
		SoundManager.Instance.ChangeMasterVolume(_slider.value);
		_slider.onValueChanged.AddListener(delegate(float val)
		{
			SoundManager.Instance.ChangeAmbientVolume(val);
		});
	}

	private void Update()
	{
	}
}
public class Crosshair : MonoBehaviour
{
	private Sprite defaultImg;

	[SerializeField]
	private Sprite interactImg;

	private GameObject player;

	private void Awake()
	{
		player = GameObject.Find("Player");
		defaultImg = GetComponent<UnityEngine.UI.Image>().sprite;
	}

	private void Update()
	{
		if (player.GetComponent<FirstPersonController>().currentInteractable != null && player.GetComponent<FirstPersonController>().interactionSphere)
		{
			GetComponent<UnityEngine.UI.Image>().sprite = interactImg;
			GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 130f);
		}
		else
		{
			GetComponent<UnityEngine.UI.Image>().sprite = defaultImg;
			GetComponent<RectTransform>().sizeDelta = new Vector2(51.2f, 51.2f);
		}
	}
}
public class DeathAnim : MonoBehaviour
{
	[SerializeField]
	private UnityEngine.UI.Image image;

	[SerializeField]
	private UnityEngine.Color black;

	[SerializeField]
	private UnityEngine.Color tempColor;

	[SerializeField]
	private float fadeSpeed;

	public float timer;

	private void Awake()
	{
		image = GetComponent<UnityEngine.UI.Image>();
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (timer < 0f)
		{
			tempColor.a -= Time.deltaTime * fadeSpeed * 6f;
			image.color = tempColor;
		}
	}

	public void Trigger()
	{
		timer = 1f;
		image.color = black;
		tempColor = black;
	}
}
public class ExtraDiegeticMusicSystem : MonoBehaviour
{
	[SerializeField]
	private AudioClip[] music;

	private float timer = 10f;

	private void Start()
	{
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (timer < 0f)
		{
			PlayRandomMusic();
		}
	}

	private void PlayRandomMusic()
	{
		timer = UnityEngine.Random.Range(150, 300);
	}
}
public class FaceCamera : MonoBehaviour
{
	private FirstPersonController player;

	private void Start()
	{
		player = GameObject.Find("Player").GetComponent<FirstPersonController>();
	}

	private void Update()
	{
		Vector3 position = GameObject.Find("Player").transform.position;
		position.y = base.transform.position.y;
		base.transform.LookAt(position);
	}
}
public class FaceCamerainverted : MonoBehaviour
{
	private void Start()
	{
	}

	private void Update()
	{
		base.transform.rotation = Quaternion.Euler(Input.GetAxis("Mouse X") * 30f, Input.GetAxis("Mouse Y") * 30f, 0f);
	}
}
public class GameManager : MonoBehaviour, ISaveable
{
	[Serializable]
	private struct SaveData
	{
		public int ticket;

		public int map;

		public int parttwo;

		public int endtrain;

		public float playTime;

		public float playerPosX;

		public float playerPosY;

		public float playerPosZ;

		public float playerRotY;

		public float camRotX;

		public int wayClimber;

		public int trainDeath;

		public int allSecrets;

		public int interactEveryone;

		public int playDog;

		public int gameUnder;

		public int impressGirl;

		public int bikeAir;

		public int escapeBabbdi;

		public int endticket;

		public int secretsFound;

		public int secret00;

		public int secret01;

		public int secret02;

		public int secret03;

		public int secret04;

		public int secret05;

		public int secret06;

		public int secret07;

		public int secret08;

		public int secret09;

		public int secret10;

		public int secret11;

		public int secret12;

		public int secret13;

		public int secret14;

		public int secret15;

		public int secret16;

		public int secret17;

		public int secret18;

		public int secret19;

		public int secret20;

		public int club;

		public int climber;

		public int propeller;

		public int blower;

		public int flashlight;

		public int soap;

		public int ball;

		public int bigball;

		public int stick;

		public int grabber;

		public int motorBike;

		public int compass;

		public int trumpet;

		public int secretfinder;
	}

	public static GameManager Instance;

	public bool gamepad;

	public AudioClip destroy;

	[Header("UI")]
	public GameObject pauseMenu;

	public GameObject confirmReturnHome;

	public GameObject notavailable;

	public GameObject returnoffice;

	public GameObject speedrunLabel;

	public GameObject ticketDisplay;

	public GameObject mapObjectDisplay;

	public GameObject mapButtonDisplay;

	public GameObject qualitySettings;

	[SerializeField]
	private GameObject dialogueBox;

	[SerializeField]
	private TMP_Text textLabel;

	[SerializeField]
	private TMP_Text name_label;

	[Header("Cutscene")]
	public Animator anim1;

	public Animator anim2;

	public GameObject bridgeCam;

	public GameObject userInterface;

	public GameObject userInterface1;

	public AudioSource bridgeAudio;

	[Header("Save Object in hands")]
	public int club;

	public int climber;

	public int propeller;

	public int blower;

	public int flashlight;

	public int soap;

	public int ball;

	public int bigball;

	public int stick;

	public int grabber;

	public int motorBike;

	public int compass;

	public int trumpet;

	public int secretfinder;

	[Header("MAINLINE")]
	public GameObject dragoStart;

	public GameObject dragoEnd;

	private bool instantiate = true;

	public Transform secondPosition;

	public GameObject continueDialogueFX;

	public int secretsFound;

	public int npcInteractedWith;

	public int maxNpc;

	[HideInInspector]
	public bool pickup;

	[HideInInspector]
	public int item;

	[HideInInspector]
	public bool inActivatedLift;

	[HideInInspector]
	public bool secretPopup;

	[HideInInspector]
	public bool mapPopup;

	[HideInInspector]
	public bool noticketPopup;

	[HideInInspector]
	public bool savePopup;

	public int requestTrain;

	[HideInInspector]
	public bool inSubway;

	[HideInInspector]
	public bool inClub;

	public bool endGame;

	public float gameTime;

	public int secondPart;

	public int haveTicket;

	public int haveMap;

	public float startTimer;

	[Header("Achievements")]
	public string lastAchievement;

	public bool wayClimber;

	public int wayClimberState;

	[SerializeField]
	private Toggle wayClimberToggle;

	public bool trainDeath;

	public int trainDeathState;

	[SerializeField]
	private Toggle trainDeathToggle;

	public bool allSecrets;

	public int allSecretsState;

	[SerializeField]
	private Toggle allSecretsToggle;

	public bool interactEveryone;

	public int interactEveryoneState;

	[SerializeField]
	private Toggle interactEveryoneToggle;

	public bool playDog;

	public int playDogState;

	[SerializeField]
	private Toggle playDogToggle;

	public bool gameUnder;

	public int gameUnderState;

	[SerializeField]
	private Toggle gameUnderToggle;

	public bool impressGirl;

	public int impressGirlState;

	[SerializeField]
	private Toggle impressGirlToggle;

	public bool bikeAir;

	public int bikeAirState;

	[SerializeField]
	private Toggle bikeAirToggle;

	public bool escapeBabbdi;

	public int escapeBabbdiState;

	[SerializeField]
	private Toggle escapeBabbdiToggle;

	public bool ticket;

	public int ticketState;

	[SerializeField]
	private Toggle ticketToggle;

	[SerializeField]
	private AudioClip[] jingle;

	public bool popup;

	[Header("Saving & Loading")]
	public GameObject[] secrets;

	public int[] secretState;

	private FirstPersonController player;

	public float bridgeTimer;

	private float lobbyTimer;

	private void Awake()
	{
		secretState = new int[secrets.Length];
		if (Instance != null)
		{
			Instance = this;
		}
		GetComponent<SaveLoadSystem>().Load();
	}

	private void Start()
	{
		if (GameObject.Find("Parameters") != null)
		{
			speedrunLabel.SetActive(GameObject.Find("Parameters").GetComponent<Parameters>().speedrun);
		}
		qualitySettings.GetComponent<SetQuality>().SetQualityLevelDropdown(3);
		player = GameObject.Find("Player").GetComponent<FirstPersonController>();
		instantiate = true;
		if (haveMap == 1)
		{
			mapObjectDisplay.SetActive(value: false);
		}
	}

	public void ReturnToMenu()
	{
		haveTicket = 0;
		secondPart = 0;
		requestTrain = 0;
		gameTime = 0f;
		GameObject.Find("Player").transform.position = new Vector3(158.621f, 42.73f, -31.9f);
		GameObject.Find("Player").transform.rotation = Quaternion.Euler(0f, 0f, 0f);
		player.rotationX = 0f;
		GetComponent<SaveLoadSystem>().Save();
		SceneManager.LoadScene("MainMenu");
	}

	private void Update()
	{
		if (secretsFound >= 21)
		{
			if (!allSecrets)
			{
				Popup();
			}
			allSecrets = true;
			allSecretsState = 1;
			lastAchievement = "Secrets master";
			SteamIntegration.Instance.UnlockAchivement("SecretsMaster");
		}
		if (Input.GetJoystickNames().Length != 0)
		{
			if (Input.GetJoystickNames()[0] != "")
			{
				gamepad = true;
			}
			else
			{
				gamepad = false;
			}
		}
		bridgeTimer -= Time.deltaTime;
		if (haveTicket == 1)
		{
			ticket = true;
		}
		else
		{
			ticket = false;
		}
		startTimer -= Time.deltaTime;
		if (!endGame && !player.pause)
		{
			gameTime += Time.deltaTime;
		}
		wayClimberToggle.isOn = wayClimber;
		allSecretsToggle.isOn = allSecrets;
		interactEveryoneToggle.isOn = interactEveryone;
		gameUnderToggle.isOn = gameUnder;
		escapeBabbdiToggle.isOn = escapeBabbdi;
		bikeAirToggle.isOn = bikeAir;
		playDogToggle.isOn = playDog;
		impressGirlToggle.isOn = impressGirl;
		trainDeathToggle.isOn = trainDeath;
		ticketToggle.isOn = ticket;
		lobbyTimer -= Time.deltaTime;
		if (lobbyTimer > 0f)
		{
			pauseMenu.SetActive(value: false);
			confirmReturnHome.SetActive(value: false);
		}
		if (secondPart == 1)
		{
			notavailable.SetActive(value: true);
			returnoffice.SetActive(value: true);
		}
		else
		{
			notavailable.SetActive(value: false);
			returnoffice.SetActive(value: false);
		}
		if (!player.pause && haveTicket == 1)
		{
			ticketDisplay.SetActive(value: true);
		}
		else
		{
			ticketDisplay.SetActive(value: false);
		}
		if (npcInteractedWith == maxNpc)
		{
			if (!interactEveryone)
			{
				Popup();
			}
			interactEveryone = true;
			interactEveryoneState = 1;
			lastAchievement = "Social Quest";
			SteamIntegration.Instance.UnlockAchivement("SocialQuest");
		}
		if (endGame && !escapeBabbdi)
		{
			escapeBabbdi = true;
			escapeBabbdiState = 1;
			SteamIntegration.Instance.UnlockAchivement("MelancholicDeparture");
			UnityEngine.Debug.Log("melcahnocl");
		}
		if (secondPart == 1 && instantiate)
		{
			Invoke("drago", 1f);
		}
		if (haveMap == 0)
		{
			mapButtonDisplay.SetActive(value: false);
		}
		else
		{
			mapButtonDisplay.SetActive(value: true);
		}
	}

	private void drago()
	{
		if (secondPart == 1 && instantiate)
		{
			instantiate = false;
			UnityEngine.Object.Destroy(dragoStart);
			GameObject obj = UnityEngine.Object.Instantiate(dragoEnd, secondPosition.position, secondPosition.rotation);
			obj.GetComponent<DialogueUI>().dialogueBox = dialogueBox;
			obj.GetComponent<DialogueUI>().textLabel = textLabel;
			obj.GetComponent<DialogueUI>().name_label = name_label;
			obj.GetComponent<DialogueUI>().continueDialogueFX = continueDialogueFX;
		}
	}

	public void AddSecret()
	{
		secretsFound++;
	}

	[ContextMenu("Popup")]
	public void Popup()
	{
		if (!endGame)
		{
			popup = true;
			SoundManager.Instance.PlaySound(jingle[UnityEngine.Random.Range(0, jingle.Length - 1)]);
		}
		else
		{
			UnityEngine.Debug.Log("qlqlq");
		}
	}

	public void ReturnHome()
	{
		if (secondPart == 0)
		{
			GameObject.Find("Player").transform.position = GameObject.Find("LobbyPosition").transform.position;
			lobbyTimer = 0.1f;
		}
	}

	public void ReturnOffice()
	{
		GameObject.Find("Player").transform.position = GameObject.Find("OfficePosition").transform.position;
		lobbyTimer = 0.1f;
	}

	public object SaveState()
	{
		SaveData saveData = default(SaveData);
		saveData.ticket = haveTicket;
		saveData.map = haveMap;
		saveData.parttwo = secondPart;
		saveData.endtrain = requestTrain;
		saveData.playTime = gameTime;
		saveData.playerPosX = GameObject.Find("Player").transform.position.x;
		saveData.playerPosY = GameObject.Find("Player").transform.position.y;
		saveData.playerPosZ = GameObject.Find("Player").transform.position.z;
		saveData.playerRotY = GameObject.Find("Player").transform.eulerAngles.y;
		saveData.camRotX = GameObject.Find("Player").GetComponent<FirstPersonController>().rotationX;
		saveData.wayClimber = wayClimberState;
		saveData.trainDeath = trainDeathState;
		saveData.allSecrets = allSecretsState;
		saveData.interactEveryone = interactEveryoneState;
		saveData.playDog = playDogState;
		saveData.gameUnder = gameUnderState;
		saveData.impressGirl = impressGirlState;
		saveData.bikeAir = bikeAirState;
		saveData.escapeBabbdi = escapeBabbdiState;
		saveData.endticket = ticketState;
		saveData.secretsFound = secretsFound;
		saveData.secret00 = secretState[0];
		saveData.secret01 = secretState[1];
		saveData.secret02 = secretState[2];
		saveData.secret03 = secretState[3];
		saveData.secret04 = secretState[4];
		saveData.secret05 = secretState[5];
		saveData.secret06 = secretState[6];
		saveData.secret07 = secretState[7];
		saveData.secret08 = secretState[8];
		saveData.secret09 = secretState[9];
		saveData.secret10 = secretState[10];
		saveData.secret11 = secretState[11];
		saveData.secret12 = secretState[12];
		saveData.secret13 = secretState[13];
		saveData.secret14 = secretState[14];
		saveData.secret15 = secretState[15];
		saveData.secret16 = secretState[16];
		saveData.secret17 = secretState[17];
		saveData.secret18 = secretState[18];
		saveData.secret19 = secretState[19];
		saveData.secret20 = secretState[20];
		saveData.club = club;
		saveData.climber = climber;
		saveData.propeller = propeller;
		saveData.blower = blower;
		saveData.flashlight = flashlight;
		saveData.soap = soap;
		saveData.ball = ball;
		saveData.bigball = bigball;
		saveData.stick = stick;
		saveData.grabber = grabber;
		saveData.motorBike = motorBike;
		saveData.compass = compass;
		saveData.trumpet = trumpet;
		saveData.secretfinder = secretfinder;
		return saveData;
	}

	public void ResetSave()
	{
		haveTicket = 0;
		haveMap = 0;
		secondPart = 0;
		requestTrain = 0;
		gameTime = 0f;
		GameObject.Find("Player").transform.position = new Vector3(160.193f, 42.1766f, -38.49f);
		GameObject.Find("Player").transform.rotation = Quaternion.Euler(0f, 0f, 0f);
		GameObject.Find("Player").GetComponent<FirstPersonController>().rotationX = 0f;
		wayClimberState = 0;
		trainDeathState = 0;
		allSecretsState = 0;
		interactEveryoneState = 0;
		playDogState = 0;
		gameUnderState = 0;
		impressGirlState = 0;
		bikeAirState = 0;
		escapeBabbdiState = 0;
		ticketState = 0;
		for (int i = 0; i < secretState.Length; i++)
		{
			secretState[i] = 0;
		}
		secretsFound = 0;
		club = 0;
		climber = 0;
		propeller = 0;
		blower = 0;
		flashlight = 0;
		soap = 0;
		ball = 0;
		bigball = 0;
		stick = 0;
		grabber = 0;
		motorBike = 0;
		compass = 0;
		trumpet = 0;
		secretfinder = 0;
	}

	public void LoadState(object state)
	{
		SaveData saveData = (SaveData)state;
		haveTicket = saveData.ticket;
		haveMap = saveData.map;
		secondPart = saveData.parttwo;
		requestTrain = saveData.endtrain;
		gameTime = saveData.playTime;
		GameObject.Find("Player").transform.position = new Vector3(saveData.playerPosX, saveData.playerPosY, saveData.playerPosZ);
		GameObject.Find("Player").transform.rotation = Quaternion.Euler(0f, saveData.playerRotY, 0f);
		GameObject.Find("Player").GetComponent<FirstPersonController>().rotationX = saveData.camRotX;
		wayClimberState = saveData.wayClimber;
		if (wayClimberState == 1)
		{
			wayClimber = true;
		}
		wayClimberState = saveData.trainDeath;
		if (wayClimberState == 1)
		{
			trainDeath = true;
		}
		allSecretsState = saveData.allSecrets;
		if (allSecretsState == 1)
		{
			allSecrets = true;
		}
		interactEveryoneState = saveData.interactEveryone;
		if (interactEveryoneState == 1)
		{
			interactEveryone = true;
		}
		playDogState = saveData.playDog;
		if (playDogState == 1)
		{
			playDog = true;
		}
		gameUnderState = saveData.gameUnder;
		if (gameUnderState == 1)
		{
			gameUnder = true;
		}
		impressGirlState = saveData.impressGirl;
		if (impressGirlState == 1)
		{
			impressGirl = true;
		}
		bikeAirState = saveData.bikeAir;
		if (bikeAirState == 1)
		{
			bikeAir = true;
		}
		escapeBabbdiState = saveData.escapeBabbdi;
		if (escapeBabbdiState == 1)
		{
			escapeBabbdi = true;
		}
		ticketState = saveData.endticket;
		if (ticketState == 1)
		{
			ticket = true;
		}
		secretsFound = saveData.secretsFound;
		secretState[0] = saveData.secret00;
		secretState[1] = saveData.secret01;
		secretState[2] = saveData.secret02;
		secretState[3] = saveData.secret03;
		secretState[4] = saveData.secret04;
		secretState[5] = saveData.secret05;
		secretState[6] = saveData.secret06;
		secretState[7] = saveData.secret07;
		secretState[8] = saveData.secret08;
		secretState[9] = saveData.secret09;
		secretState[10] = saveData.secret10;
		secretState[11] = saveData.secret11;
		secretState[12] = saveData.secret12;
		secretState[13] = saveData.secret13;
		secretState[14] = saveData.secret14;
		secretState[15] = saveData.secret15;
		secretState[16] = saveData.secret16;
		secretState[17] = saveData.secret17;
		secretState[18] = saveData.secret18;
		secretState[19] = saveData.secret19;
		secretState[20] = saveData.secret20;
		club = saveData.club;
		climber = saveData.climber;
		propeller = saveData.propeller;
		blower = saveData.blower;
		flashlight = saveData.flashlight;
		soap = saveData.soap;
		ball = saveData.ball;
		bigball = saveData.bigball;
		stick = saveData.stick;
		grabber = saveData.grabber;
		motorBike = saveData.motorBike;
		compass = saveData.compass;
		trumpet = saveData.trumpet;
		secretfinder = saveData.secretfinder;
	}
}
public class HideCrosshair : MonoBehaviour
{
	private bool hide = true;

	private void Start()
	{
	}

	private void Update()
	{
		if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown("l"))
		{
			hide = !hide;
		}
		base.gameObject.GetComponent<UnityEngine.UI.Image>().enabled = hide;
	}
}
public class Intro : MonoBehaviour
{
	[SerializeField]
	private UnityEngine.UI.Image image;

	[SerializeField]
	private UnityEngine.Color tempColor;

	[SerializeField]
	private float fadeSpeed;

	public float timer;

	private void Awake()
	{
		image = GetComponent<UnityEngine.UI.Image>();
	}

	private void Start()
	{
		tempColor = image.color;
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		if (timer < -4f)
		{
			tempColor.a -= Time.deltaTime * fadeSpeed * 6f;
			image.color = tempColor;
		}
		else if (timer < -3f)
		{
			tempColor.a -= Time.deltaTime * fadeSpeed * 4f;
			image.color = tempColor;
		}
		else if (timer < -2f)
		{
			tempColor.a -= Time.deltaTime * fadeSpeed * 2f;
			image.color = tempColor;
		}
		else if (timer < 0f)
		{
			tempColor.a -= Time.deltaTime * fadeSpeed;
			image.color = tempColor;
		}
	}
}
public class Library : MonoBehaviour
{
	[SerializeField]
	private TMP_Text text;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		text.text = "You collected " + gameManager.secretsFound + "/21 secret objects.";
	}
}
public class MainMenu : MonoBehaviour
{
	[SerializeField]
	private AudioClip pointer;

	[SerializeField]
	private AudioClip click;

	[SerializeField]
	private AudioSource audio;

	public void PlayGame()
	{
		SceneManager.LoadScene("Scene_A");
	}

	public void QuitGame()
	{
		Application.Quit();
	}

	public void OnPointerEnter()
	{
		audio.PlayOneShot(pointer);
	}

	public void OnClick()
	{
		audio.PlayOneShot(click);
	}

	private void Start()
	{
		GameObject.Find("SavingOptions").GetComponent<SaveLoadSystem>().Load();
	}
}
public class MasterVolume : MonoBehaviour
{
	[SerializeField]
	private Slider _slider;

	private void Awake()
	{
		_slider.value = AudioListener.volume;
	}

	private void Start()
	{
		SoundManager.Instance.ChangeMasterVolume(_slider.value);
		_slider.onValueChanged.AddListener(delegate(float val)
		{
			SoundManager.Instance.ChangeMasterVolume(val);
		});
	}

	private void Update()
	{
	}
}
public class MenuActions : MonoBehaviour
{
	[SerializeField]
	private AudioClip pointer;

	[SerializeField]
	private AudioClip click;

	public void OnPointerEnter()
	{
		SoundManager.Instance.PlaySound(pointer);
	}

	public void OnClick()
	{
		SoundManager.Instance.PlaySound(click);
	}
}
public class MouseSensitivitySlider : MonoBehaviour
{
	[SerializeField]
	private Slider _slider;

	private void Start()
	{
	}

	private void Update()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().lookSpeedX = _slider.value;
		GameObject.Find("Player").GetComponent<FirstPersonController>().lookSpeedY = _slider.value;
	}
}
public class MusicZone : MonoBehaviour
{
	public AudioClip music;

	public float countown;

	public float timer;

	private void Update()
	{
		timer -= Time.deltaTime;
	}
}
public class Outro : MonoBehaviour
{
	[SerializeField]
	private UnityEngine.UI.Image image;

	[SerializeField]
	private UnityEngine.Color tempColor;

	[SerializeField]
	private float fadeSpeed;

	public TMP_Text runTime;

	public GameObject title;

	public GameObject text;

	public GameObject text2;

	public GameObject nextState;

	public float timer = 3f;

	private float initTimer;

	private GameManager gameManager;

	private void Awake()
	{
	}

	private void Start()
	{
		tempColor = image.color;
		initTimer = timer;
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (GameObject.Find("GameManager").GetComponent<GameManager>().endGame && timer > -4f)
		{
			timer -= Time.deltaTime;
			if (timer < 5f)
			{
				tempColor.a += Time.deltaTime * fadeSpeed;
				image.color = tempColor;
			}
			if (timer < -1f)
			{
				title.SetActive(value: true);
			}
			if (timer < -2f)
			{
				text.SetActive(value: true);
			}
			if (timer < -3f)
			{
				text2.SetActive(value: true);
			}
			if (timer < -4f)
			{
				nextState.SetActive(value: true);
			}
			runTime.text = "You completed the game in " + GameObject.Find("GameManager").GetComponent<GameManager>().gameTime + " seconds.";
		}
	}
}
public class PauseMenu : MonoBehaviour
{
	public void QuitGame()
	{
		SceneManager.LoadScene("MainMenu");
	}
}
public interface ISaveable
{
	object SaveState();

	void LoadState(object state);
}
public class SaveableEntity : MonoBehaviour
{
	[SerializeField]
	private string id;

	public string Id => id;

	[ContextMenu("Generate Id")]
	private void GenerateId()
	{
		id = Guid.NewGuid().ToString();
	}

	public object SaveState()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		ISaveable[] components = GetComponents<ISaveable>();
		foreach (ISaveable saveable in components)
		{
			dictionary[saveable.GetType().ToString()] = saveable.SaveState();
		}
		return dictionary;
	}

	public void LoadState(object state)
	{
		Dictionary<string, object> dictionary = (Dictionary<string, object>)state;
		ISaveable[] components = GetComponents<ISaveable>();
		foreach (ISaveable saveable in components)
		{
			string key = saveable.GetType().ToString();
			if (dictionary.TryGetValue(key, out var value))
			{
				saveable.LoadState(value);
			}
		}
	}
}
public class SaveLoadSystem : MonoBehaviour
{
	public static bool saveLoaded;

	public string SavePath => Application.persistentDataPath + "/save.dababi";

	[ContextMenu("Save")]
	public void Save()
	{
		Dictionary<string, object> state = LoadFile();
		SaveState(state);
		SaveFile(state);
	}

	[ContextMenu("Load")]
	public void Load()
	{
		Dictionary<string, object> state = LoadFile();
		LoadState(state);
	}

	public void SaveFile(object state)
	{
		using FileStream serializationStream = File.Open(SavePath, FileMode.Create);
		new BinaryFormatter().Serialize(serializationStream, state);
	}

	private Dictionary<string, object> LoadFile()
	{
		if (!File.Exists(SavePath))
		{
			UnityEngine.Debug.Log("No save file found");
			return new Dictionary<string, object>();
		}
		using FileStream serializationStream = File.Open(SavePath, FileMode.Open);
		return (Dictionary<string, object>)new BinaryFormatter().Deserialize(serializationStream);
	}

	private void SaveState(Dictionary<string, object> state)
	{
		SaveableEntity[] array = UnityEngine.Object.FindObjectsOfType<SaveableEntity>();
		foreach (SaveableEntity saveableEntity in array)
		{
			state[saveableEntity.Id] = saveableEntity.SaveState();
		}
	}

	private void LoadState(Dictionary<string, object> state)
	{
		SaveableEntity[] array = UnityEngine.Object.FindObjectsOfType<SaveableEntity>();
		foreach (SaveableEntity saveableEntity in array)
		{
			if (state.TryGetValue(saveableEntity.Id, out var value))
			{
				UnityEngine.Debug.Log(saveableEntity.ToString());
				saveableEntity.LoadState(value);
			}
		}
	}

	public void Update()
	{
	}
}
public class ScreenShot : MonoBehaviour
{
	private string directoryName = "Screenshots";

	private string screenshotName = "";

	[SerializeField]
	private AudioClip screenshotClip;

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F12))
		{
			TakeScreenshot();
		}
	}

	private string RandomStringGenerator(int length)
	{
		string text = "ABDIBB";
		string text2 = "";
		for (int i = 0; i < length; i++)
		{
			text2 += text[UnityEngine.Random.Range(0, length)];
		}
		return text2;
	}

	public void TakeScreenshot()
	{
		string text = DateTime.Now.ToString("dd-MMMM-yyyy HHmmss");
		SoundManager.Instance.PlaySound(screenshotClip);
		screenshotName = RandomStringGenerator(6);
		ScreenCapture.CaptureScreenshot(Path.Combine(Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Application.productName, directoryName)).FullName, screenshotName + "(" + text + ").png"));
	}
}
public class SecretObjectState : MonoBehaviour
{
	[SerializeField]
	private bool collected;

	[SerializeField]
	private int objectIndex;

	private void Start()
	{
		if (GameObject.Find("GameManager").GetComponent<GameManager>().secretState[objectIndex] == 1)
		{
			collected = true;
		}
		else
		{
			collected = false;
		}
		if (collected)
		{
			base.gameObject.SetActive(value: false);
		}
		else
		{
			base.gameObject.SetActive(value: true);
		}
	}
}
public class SetAmbientVolume : MonoBehaviour
{
	[SerializeField]
	private AudioSource audio;

	private AudioSource proutit;

	private void Awake()
	{
		if (GetComponent<AudioSource>() != null)
		{
			audio = GetComponent<AudioSource>();
		}
		proutit = GameObject.Find("AmbientSource").GetComponent<AudioSource>();
	}

	private void Update()
	{
		audio.volume = proutit.volume;
	}
}
public class SetLift : MonoBehaviour
{
	public GameObject lift;

	private void Start()
	{
	}

	private void Update()
	{
	}
}
public class SetMusicVolume : MonoBehaviour
{
	[SerializeField]
	private AudioSource audio;

	private AudioSource proutit;

	private void Awake()
	{
		if (GetComponent<AudioSource>() != null)
		{
			audio = GetComponent<AudioSource>();
		}
		proutit = GameObject.Find("MusicSource").GetComponent<AudioSource>();
	}

	private void Update()
	{
		audio.volume = proutit.volume;
	}
}
public class SetQuality : MonoBehaviour
{
	[SerializeField]
	private TMP_Dropdown qualityDropdown;

	public void SetQualityLevelDropdown(int index)
	{
		index = qualityDropdown.value;
		QualitySettings.SetQualityLevel(index, applyExpensiveChanges: false);
	}

	private void Start()
	{
	}

	private void Update()
	{
	}
}
public class SetSFXVolume : MonoBehaviour
{
	[SerializeField]
	private AudioSource audio;

	private AudioSource proutit;

	private void Awake()
	{
		if (GetComponent<AudioSource>() != null)
		{
			audio = GetComponent<AudioSource>();
		}
		proutit = GameObject.Find("SoundEffectSource").GetComponent<AudioSource>();
	}

	private void Update()
	{
		audio.volume = proutit.volume;
	}
}
public class SFXSlider : MonoBehaviour
{
	[SerializeField]
	private Slider _slider;

	private void Awake()
	{
		_slider.value = SoundManager.Instance._effectsSource.volume;
	}

	private void Start()
	{
		SoundManager.Instance.ChangeMasterVolume(_slider.value);
		_slider.onValueChanged.AddListener(delegate(float val)
		{
			SoundManager.Instance.ChangeEffectVolume(val);
		});
	}

	private void Update()
	{
	}
}
public class SoundManager : MonoBehaviour
{
	public static SoundManager Instance;

	[SerializeField]
	public AudioSource _musicSource;

	[SerializeField]
	public AudioSource _effectsSource;

	[SerializeField]
	public AudioSource _ambientSource;

	[SerializeField]
	public AudioSource _musicShotSource;

	[SerializeField]
	public AudioSource _indoorSource;

	[SerializeField]
	public AudioSource _outdoorSource;

	[SerializeField]
	private float blendSpeed;

	private bool outdoor = true;

	private bool indoor;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void Update()
	{
	}

	public void PlaySound(AudioClip clip)
	{
		_effectsSource.PlayOneShot(clip);
	}

	public void PlayMusicShot(AudioClip clip)
	{
		_musicShotSource.PlayOneShot(clip);
	}

	public void PlayAmbientSound(AudioClip clip)
	{
		_ambientSource.PlayOneShot(clip);
	}

	public void PlayContinuousSound(AudioClip clop)
	{
		_effectsSource.clip = clop;
		_effectsSource.Play();
	}

	public void PlayMusic(AudioClip clip, int loop)
	{
		_musicSource.PlayOneShot(clip);
		if (loop == 1)
		{
			_musicSource.loop = true;
		}
		if (loop == 0)
		{
			_musicSource.loop = false;
		}
	}

	public void StopAllMusic()
	{
		_musicSource.Stop();
	}

	public void StopSound()
	{
		_effectsSource.Stop();
	}

	public void ChangeMasterVolume(float value)
	{
		AudioListener.volume = value;
	}

	public void ChangeMusicVolume(float value)
	{
		_musicSource.volume = value;
	}

	public void ChangeEffectVolume(float value)
	{
		_effectsSource.volume = value;
	}

	public void ChangeAmbientVolume(float value)
	{
		_ambientSource.volume = value;
		if (indoor)
		{
			_indoorSource.volume = _ambientSource.volume;
		}
		if (outdoor)
		{
			_outdoorSource.volume = _ambientSource.volume;
		}
	}

	public void IndoorBlend()
	{
		indoor = true;
		outdoor = false;
		_indoorSource.volume = Mathf.Lerp(_indoorSource.volume, _ambientSource.volume, blendSpeed * Time.deltaTime);
		_outdoorSource.volume = Mathf.Lerp(_outdoorSource.volume, 0f, blendSpeed * Time.deltaTime);
	}

	public void OutdoorBlend()
	{
		indoor = false;
		outdoor = true;
		_indoorSource.volume = Mathf.Lerp(_indoorSource.volume, 0f, blendSpeed * Time.deltaTime);
		_outdoorSource.volume = Mathf.Lerp(_outdoorSource.volume, _ambientSource.volume, blendSpeed * Time.deltaTime);
	}

	public void ToggleEffects()
	{
		_effectsSource.mute = !_effectsSource.mute;
	}

	public void ToggleMusic()
	{
		_musicSource.mute = !_musicSource.mute;
	}
}
public class SpeedrunClock : MonoBehaviour
{
	private void Start()
	{
	}

	private void Update()
	{
	}
}
public class SteamIntegration : MonoBehaviour
{
	public static SteamIntegration Instance;

	private bool connected;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		try
		{
			SteamClient.Init(2240530u);
			PrintYourName();
			connected = true;
		}
		catch (Exception message)
		{
			UnityEngine.Debug.Log(message);
		}
	}

	private void PrintYourName()
	{
		UnityEngine.Debug.Log("Steam Marche");
	}

	[ContextMenu("IsThisAchievementUnlocked")]
	public void IsThisAchievementUnlocked(string id)
	{
		new Achievement(id);
	}

	[ContextMenu("UnlockAchievement")]
	public void UnlockAchivement(string id)
	{
		if (connected)
		{
			Achievement achievement = new Achievement(id);
			if (!achievement.State)
			{
				achievement.Trigger();
			}
			UnityEngine.Debug.Log("AchievementUnlocked : " + id);
		}
	}

	[ContextMenu("ClearAchievementStatus")]
	public void ClearAchievementStatus(string id)
	{
		new Achievement(id).Clear();
	}
}
public class Timer : MonoBehaviour
{
	public TextMeshProUGUI textBox;

	private GameManager gameManager;

	private void Start()
	{
		textBox.text = GameObject.Find("GameManager").GetComponent<GameManager>().gameTime.ToString("F2");
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		textBox.text = gameManager.gameTime.ToString("F2");
	}
}
public class ToggleAudio : MonoBehaviour
{
	[SerializeField]
	private bool _toggleMusic;

	[SerializeField]
	private bool _toggleEffects;

	public void Toggle()
	{
		SoundManager.Instance.ToggleEffects();
		SoundManager.Instance.ToggleMusic();
	}
}
public class ToggleSpeedrunMode : MonoBehaviour
{
	public GameObject speedrunLabel;

	private SavingOptions save;

	private void Start()
	{
		if (GameObject.Find("Parameters") != null)
		{
			GetComponent<Toggle>().isOn = GameObject.Find("Parameters").GetComponent<Parameters>().speedrun;
		}
		save = GameObject.Find("SavingOptions").GetComponent<SavingOptions>();
	}

	private void Update()
	{
		speedrunLabel.SetActive(GetComponent<Toggle>().isOn);
		if (GetComponent<Toggle>().isOn)
		{
			save.speedrun = 1;
		}
		else
		{
			save.speedrun = 0;
		}
		GameObject.Find("Parameters").GetComponent<Parameters>().speedrun = GetComponent<Toggle>().isOn;
	}
}
public class UI : MonoBehaviour
{
	private void Awake()
	{
		base.gameObject.SetActive(value: true);
	}

	private void Update()
	{
	}
}
public class VolumeSlider : MonoBehaviour
{
	[SerializeField]
	private Slider _slider;

	private void Awake()
	{
		_slider.value = SoundManager.Instance._musicSource.volume;
	}

	private void Start()
	{
		SoundManager.Instance.ChangeMasterVolume(_slider.value);
		_slider.onValueChanged.AddListener(delegate(float val)
		{
			SoundManager.Instance.ChangeMusicVolume(val);
		});
	}

	private void Update()
	{
	}
}
public class TextPrompt : MonoBehaviour
{
	[SerializeField]
	private TMP_Text textLabel;

	[SerializeField]
	private string normalText;

	[SerializeField]
	private string gamepadText;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Awake()
	{
	}

	private void Update()
	{
		if (gameManager.gamepad)
		{
			textLabel.text = gamepadText;
		}
		else
		{
			textLabel.text = normalText;
		}
	}
}
public class TV : Interactable
{
	[SerializeField]
	private Material on;

	[SerializeField]
	private Material off;

	[SerializeField]
	private GameObject tv;

	[SerializeField]
	private AudioClip tvClip;

	[SerializeField]
	private int matIndex = 3;

	private Material[] mats;

	public bool turnedOn = true;

	private new void Awake()
	{
	}

	public override void OnFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().jukeboxPopup.SetActive(value: true);
	}

	public override void OnInteract()
	{
		SoundManager.Instance.PlaySound(tvClip);
		mats = tv.GetComponent<Renderer>().materials;
		turnedOn = !turnedOn;
		if (turnedOn)
		{
			mats[matIndex] = on;
		}
		else if (!turnedOn)
		{
			mats[matIndex] = off;
		}
		tv.GetComponent<Renderer>().materials = mats;
	}

	public override void OnLoseFocus()
	{
		GameObject.Find("Player").GetComponent<FirstPersonController>().jukeboxPopup.SetActive(value: false);
	}
}
public class TVSound : MonoBehaviour
{
	[SerializeField]
	private GameObject tv;

	private AudioSource audio;

	private void Awake()
	{
		audio = GetComponent<AudioSource>();
	}

	private void Update()
	{
		audio.mute = !tv.GetComponent<TV>().turnedOn;
	}
}
public class Wife : MonoBehaviour
{
	[SerializeField]
	private GameObject corpseParticles;

	[SerializeField]
	private GameObject corpseParticles2;

	[SerializeField]
	private AudioSource flies;

	private GameManager gameManager;

	private void Start()
	{
		gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
	}

	private void Update()
	{
		if (gameManager.secondPart == 1)
		{
			base.gameObject.layer = 0;
			if (corpseParticles != null)
			{
				corpseParticles.SetActive(value: true);
			}
			if (corpseParticles2 != null)
			{
				corpseParticles2.SetActive(value: true);
			}
			flies.mute = false;
		}
		else
		{
			if (corpseParticles != null)
			{
				corpseParticles.SetActive(value: false);
			}
			if (corpseParticles2 != null)
			{
				corpseParticles2.SetActive(value: false);
			}
			flies.mute = true;
		}
	}
}
public class SpeedrunMenu : MonoBehaviour
{
	private void Start()
	{
		if (GameObject.Find("Parameters") != null)
		{
			GetComponent<Toggle>().isOn = GameObject.Find("Parameters").GetComponent<Parameters>().speedrun;
		}
	}

	private void Update()
	{
	}
}
