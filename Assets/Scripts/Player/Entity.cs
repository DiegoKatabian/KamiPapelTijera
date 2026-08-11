using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Entity : MonoBehaviour
{
    [SerializeField]
    protected float _hp = 100;
    [SerializeField]
    protected float _maxSpeed = 1.5f;
    [SerializeField]
    protected float _attackDamage = 10;

    public Entity SetHP(float desiredHp)
    {
        _hp = desiredHp;
        return this;
    }
    public Entity SetSpeed(float desiredSpeed)
    {
        _maxSpeed = desiredSpeed;
        return this;
    }
    public Entity SetAttackDamage(float desiredAtkDmg)
    {
        _attackDamage = desiredAtkDmg;
        return this;
    }

    public virtual void TakeDamage(float dmg)
    {
        TakeDamage(dmg, DeathCause.Generic);
    }

    //overload con causa: si este daño termina matando, Die() se entera de que lo mato (rio, rocoso, etc)
    public virtual void TakeDamage(float dmg, DeathCause cause)
    {
        //print("entity: recibi " + dmg + " damage");
        _hp -= dmg;
        if (_hp <= 0)
        {
            Die(cause);
        }
    }
    public virtual void Die(DeathCause cause = DeathCause.Generic)
    {
        //print("entity: me mori");
        Destroy(gameObject);
    }

}
