using DevConsole;
using UnityEngine;
using System;
using System.Collections.Generic;

public class GameConsolePlugin : IConsolePlugin
{
    public static ConVarInt cvCoins;

    public void RegisterCommands()
    {
        cvCoins = new ConVarInt("wealth", 10, "Wealth on your pile",
            getter: () => CoinHill.coins,
            setter: (val) => {
                if (val < 0) throw new Exception("You're not as poor as I am.");
                CoinHill.coins = val;
            },
            onChange: (val) => CoinHill.instance.UpdateText()
        );

        new ConVarFloat("fb_damage", 2.5f, "Base fire damage", () => FireBullet.fireDamage, v => FireBullet.fireDamage = v);
        new ConVarFloat("fb_damage_wave", 0f, "Fire damage bonus for wave", () => FireBullet.fireDamageForWave, v => FireBullet.fireDamageForWave = v);
        new ConVarFloat("fb_speed", 6f, "Bullet speed", () => FireBullet.fireSpeed, v => FireBullet.fireSpeed = v);
        new ConVarFloat("fb_percent_dmg", 0f, "Percentage damage", () => FireBullet.percentageDamage, v => FireBullet.percentageDamage = v);
        new ConVarFloat("fb_armor_melt", 0f, "Armor melt per hit", () => FireBullet.armorMelt, v => FireBullet.armorMelt = v);
        new ConVarFloat("fb_size", 1f, "Bullet scale", () => FireBullet.size, v => FireBullet.size = v);
        new ConVarFloat("fb_knockback", 0f, "Knockback force", () => FireBullet.knockback, v => FireBullet.knockback = v);
        new ConVarFloat("fb_magic_dmg", 0.5f, "Magic damage", () => FireBullet.magicDamage, v => FireBullet.magicDamage = v);
        new ConVarFloat("fb_magic_area", 0f, "Magic area size", () => FireBullet.magicArea, v => FireBullet.magicArea = v);
        new ConVarFloat("fb_freeze_time", 0f, "Freeze duration", () => FireBullet.freezeTime, v => FireBullet.freezeTime = v);
        new ConVarFloat("fb_speed_dmg", 0f, "Speed scaling damage", () => FireBullet.speedDamage, v => FireBullet.speedDamage = v);
        new ConVarFloat("fb_slowing", 0f, "Slow percentage", () => FireBullet.slowing, v => FireBullet.slowing = v);

        new ConVarInt("shoot_magma_chance", 0, "Magma shot chance", () => Shooting.magmachance, v => Shooting.magmachance = v);
        new ConVarBool("shoot_global_frozen", false, "Is everything frozen", () => Shooting.IsGlobalFrozen, v => Shooting.IsGlobalFrozen = v);
        new ConVarFloat("shoot_charge_bonus", 0f, "Bonus charge per shot", () => Shooting.chargeBonus, v => Shooting.chargeBonus = v);

        new ConVarInt("gold_rain_chance", 0, "Gold rain chance", () => GoldRain.chance, v => GoldRain.chance = v);
        new ConVarFloat("enemy_exp_chance", 0f, "Enemy death explosion chance", () => EnemyBase.explosionChance, v => EnemyBase.explosionChance = v);
        new ConVarFloat("enemy_exp_area", 0f, "Enemy death explosion area", () => EnemyBase.explosionArea, v => EnemyBase.explosionArea = v);
        new ConVarInt("enemy_elite_chance", 0, "Elite spawn chance", () => EnemyBase.eliteChance, v => EnemyBase.eliteChance = v);
        new ConVarInt("enemy_elite_exp_mult", 0, "Elite exp multiplier", () => EnemyBase.eliteExpMult, v => EnemyBase.eliteExpMult = v);
        new ConVarInt("shield_killed", 0, "Enemies killed by shield", () => Shield.enemyKilled, v => Shield.enemyKilled = v);

        new ConVarBool("spawn_enemies", true, "Spawn enemies?", () => EnemySpawner.instance.enabled, v => EnemySpawner.instance.enabled = v);

        CommandRegistry.Register("spawn", "/spawn <object> [count]", (args, pool) =>
        {
            if (args.Length == 0) throw new Exception("Provide object name.");
            string objName = pool[args[0].StringIndex];

            int count = 1;
            if (args.Length > 1 && !args[1].TryGetInt(pool, out count))
                count = 1;
            
            AddressablesLoader.LoadAsync<GameObject>(objName, prefab =>
            {
                if (prefab == null) return;

                for (int i = 0; i < count; i++)
                    UnityEngine.Object.Instantiate(prefab, Game.MainCamera.ScreenToWorldPoint(Input.mousePosition), Quaternion.identity);

                DebugConsole.Log($"Spawned {count}x {objName}");
            });
        },
        hintProvider: (argIndex, currentArg) => 
        {
            if (argIndex == 0) return AddressablesLoader.GetSuggestionsForType<GameObject>(currentArg);
            if (argIndex == 1) return new List<string> { "1", "5", "10", "50" };
            return null;
        });

        CommandRegistry.Register("summon", "/summon <enemy_name> [count]", (args, pool_strings) =>
        {
            if (EnemySpawner.instance == null) throw new Exception("EnemySpawner is not active on this scene.");
            if (args.Length == 0) throw new Exception("Provide enemy name.");
            
            string objName = pool_strings[args[0].StringIndex];
            int count = 1;
            if (args.Length > 1 && !args[1].TryGetInt(pool_strings, out count))
                count = 1;
            
            AddressablesLoader.LoadAsync<GameObject>(objName, prefab =>
            {
                if (prefab == null) return;
                
                if (prefab.GetComponent<EnemyBase>() == null)
                {
                    DebugConsole.LogError($"Prefab '{objName}' is not an enemy (Missing EnemyBase component).");
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    Vector2 spawnPos = EnemySpawner.instance.GetSpawnPosition();
                    
                    EnemySpawner.instance.SpawnSpecificEnemy(prefab, spawnPos);
                }

                DebugConsole.Log($"Summoned {count}x {objName} via Spawner at random borders.");
            });
        },
        hintProvider: (argIndex, currentArg) => 
        {
            if (argIndex == 0) return AddressablesLoader.GetSuggestionsForType<GameObject>(currentArg);
            if (argIndex == 1) return new List<string> { "1", "5", "10", "50" };
            return null;
        });

        CommandRegistry.Register("upgrade", "/upgrade <upgrade_name> [count]", (args, pool) =>
        {
            if (!Upgrades.instance) throw new Exception("Game isn't started yet.");
            if (args.Length == 0) throw new Exception("Provide upgrade name.");
            string upgrName = pool[args[0].StringIndex];
            int count = 1;
            if (args.Length > 1 && !args[1].TryGetInt(pool, out count))
                count = 1;
            
            AddressablesLoader.LoadAsync<UpgradeData>(upgrName, upgrade =>
            {
                if (upgrade == null) return;

                for (int i = 0; i < count; i++)
                    Upgrades.instance.GiveUpgrade(upgrade);

                DebugConsole.Log($"Upgrade {upgrName} given {count}x times");
            });
        },
        hintProvider: (argIndex, currentArg) => 
        {
            if (argIndex == 0) return AddressablesLoader.GetSuggestionsForType<UpgradeData>(currentArg);
            if (argIndex == 1) return new List<string> { "1", "5", "10", "50" };
            return null;
        });

        CommandRegistry.Register("wave", "/wave [count]", (args, pool) =>
        {
            if (!EnemySpawner.instance) throw new Exception("Game isn't started yet.");
            int count = 1;
            if (args.Length > 0 && !args[0].TryGetInt(pool, out count))
                count = 1;
            
            for (int i = 0; i < count; i++)
                EnemySpawner.instance.SpawnWave();
        },
        hintProvider: (argIndex, currentArg) => 
        {
            if (argIndex == 0) return new List<string> { "1", "5", "10", "50" };
            return null;
        });

        CommandRegistry.Register("lvlup", "/lvlup [count]", (args, pool) =>
        {
            if (!Upgrades.instance) throw new Exception("Game isn't started yet.");
            int count = 1;
            if (args.Length > 0 && !args[0].TryGetInt(pool, out count))
                count = 1;
            var start = Upgrades.instance.Level;
            
            for (int i = 0; i < count; i++)
                Upgrades.instance.LevelUp();
            
            DebugConsole.Log($"Level {start} -> {Upgrades.instance.Level}");
        },
        hintProvider: (argIndex, currentArg) => 
        {
            if (argIndex == 0) return new List<string> { "1", "5", "10", "50" };
            return null;
        });
    }
}