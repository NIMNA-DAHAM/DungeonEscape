using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonObjectRandomizer
{
    private const float WallClearance = 0.75f;
    private const float MinimumPlayerToKeyDistance = 5f;
    private const float MinimumKeyToExitDistance = 7f;
    private const float MinimumPlayerToExitDistance = 5f;
    private const float MinimumSecondKeyDistance = 4f;
    private const float MinimumExtraLifeObjectiveDistance = 2f;
    private const float CircularPathClearance = 0.85f;

    public static void RandomizePositions(int level)
    {
        int requiredEnemyCount = level == 2 || level >= 4 ? 2 : 1;
        int requiredKeyCount = level >= 3 ? 2 : 1;
        int requiredExtraLifeCount = level == 3 ? 2 : 1;
        bool bonusLife = level >= 4;
        EnsureEnemyCount(requiredEnemyCount);
        EnsureKeyCount(requiredKeyCount);
        EnsureExtraLifeCount(requiredExtraLifeCount);

        PlayerMovement[] players = UnityEngine.Object.FindObjectsByType<PlayerMovement>();
        KeyCollect[] keys = UnityEngine.Object.FindObjectsByType<KeyCollect>();
        Array.Sort(keys, (first, second) => string.CompareOrdinal(first.name, second.name));
        ExtraLifePickup[] extraLives = UnityEngine.Object.FindObjectsByType<ExtraLifePickup>();
        Array.Sort(extraLives, (first, second) => string.CompareOrdinal(first.name, second.name));
        ExitDoor[] exits = UnityEngine.Object.FindObjectsByType<ExitDoor>();
        EnemyMovement[] enemies = UnityEngine.Object.FindObjectsByType<EnemyMovement>();
        Array.Sort(enemies, (first, second) => string.CompareOrdinal(first.name, second.name));
        GameObject walls = GameObject.Find("Walls");
        if (players.Length == 0 || keys.Length < requiredKeyCount || exits.Length == 0 ||
            enemies.Length < requiredEnemyCount || extraLives.Length < requiredExtraLifeCount || walls == null)
        {
            Debug.LogError("[DungeonEscape] Random placement requires a Player, the level's keys, enemies, and health pickups, an ExitDoor, and Walls.");
            return;
        }

        if (!ConfigureMazeWalls(walls, level))
        {
            return;
        }

        Collider2D[] wallColliders = walls.GetComponentsInChildren<Collider2D>();
        if (wallColliders.Length == 0)
        {
            Debug.LogError("[DungeonEscape] Random placement requires colliders on the boundary walls.");
            return;
        }

        if (level == 5)
        {
            RandomizeMazePositions(players[0], keys, exits[0], enemies, extraLives);
            return;
        }

        Bounds wallBounds = wallColliders[0].bounds;
        for (int i = 1; i < wallColliders.Length; i++)
        {
            wallBounds.Encapsulate(wallColliders[i].bounds);
        }

        int minX = Mathf.CeilToInt(wallBounds.min.x + WallClearance);
        int maxX = Mathf.FloorToInt(wallBounds.max.x - WallClearance);
        int minY = Mathf.CeilToInt(wallBounds.min.y + WallClearance);
        int maxY = Mathf.FloorToInt(wallBounds.max.y - WallClearance);
        bool circularPatrol = level == 3;
        Vector2 circleCenter = new Vector2(wallBounds.center.x, wallBounds.center.y);
        float circleRadius = Mathf.Min(wallBounds.extents.x, wallBounds.extents.y) - 1.15f;

        List<Vector2Int> positions = new List<Vector2Int>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                positions.Add(new Vector2Int(x, y));
            }
        }

        if (circularPatrol)
        {
            positions.RemoveAll(position => IsNearCircularPath(position, circleCenter, circleRadius));
        }

        System.Random random = new System.Random(Guid.NewGuid().GetHashCode() ^ Environment.TickCount);
        Shuffle(positions, random);

        PlayerMovement player = players[0];
        KeyCollect key = keys[0];
        ExitDoor exit = exits[0];
        if (!TryChooseObjectivePositions(positions, random, minX, maxX, minY, maxY, enemies.Length > 1,
                circularPatrol, circleCenter, circleRadius,
                out Vector2Int playerPosition,
                out Vector2Int keyPosition, out Vector2Int exitPosition,
                out Vector2Int patrolStart, out Vector2Int patrolEnd,
                out Vector2Int secondPatrolStart, out Vector2Int secondPatrolEnd))
        {
            Debug.LogError("[DungeonEscape] Could not find safe positions for the level objectives and enemy patrols.");
            return;
        }

        Vector2Int secondKeyPosition = default;
        if (requiredKeyCount > 1 &&
            !TryChooseSecondKeyPosition(positions, random, playerPosition, keyPosition, exitPosition, out secondKeyPosition))
        {
            Debug.LogError("[DungeonEscape] Could not find a safe, separate position for the second key.");
            return;
        }

        Vector2Int extraLifePosition = default;
        if (bonusLife &&
            !TryChooseExtraLifePosition(positions, random, playerPosition, keyPosition, secondKeyPosition,
                exitPosition, out extraLifePosition))
        {
            Debug.LogError("[DungeonEscape] Could not find a safe position for Level 4's extra-life pickup.");
            return;
        }

        List<Transform> objects = new List<Transform>();
        AddTransforms(objects, players);
        AddTransforms(objects, UnityEngine.Object.FindObjectsByType<CoinCollect>());
        AddTransforms(objects, UnityEngine.Object.FindObjectsByType<TreasureChestCollect>());
        AddTransforms(objects, UnityEngine.Object.FindObjectsByType<ExtraLifePickup>());
        AddTransforms(objects, UnityEngine.Object.FindObjectsByType<TimePickup>());
        AddTransforms(objects, UnityEngine.Object.FindObjectsByType<EnemyMovement>());
        AddTransforms(objects, keys);
        AddTransforms(objects, exits);

        int dedicatedExtraLifeCount = bonusLife ? 1 : 0;
        int remainingObjects = objects.Count - 3 - enemies.Length - (keys.Length - 1) - dedicatedExtraLifeCount;
        int reservedPositions = 3 + (keys.Length - 1) + dedicatedExtraLifeCount +
                                (circularPatrol ? 0 : enemies.Length * 2);
        if (positions.Count - reservedPositions < remainingObjects)
        {
            Debug.LogError("[DungeonEscape] There are not enough open positions for all gameplay objects.");
            return;
        }

        player.SetSpawnPosition(playerPosition);
        SetPosition(key.transform, keyPosition);
        if (requiredKeyCount > 1)
        {
            SetPosition(keys[1].transform, secondKeyPosition);
        }
        if (bonusLife)
        {
            SetPosition(extraLives[0].transform, extraLifePosition);
        }

        SetPosition(exit.transform, exitPosition);

        if (circularPatrol)
        {
            enemies[0].SetCircularPatrol(circleCenter, circleRadius, (float)random.NextDouble() * 360f);
        }
        else
        {
            enemies[0].SetPatrolPositions(patrolStart, patrolEnd);
            if (enemies.Length == 2)
            {
                enemies[1].SetPatrolPositions(secondPatrolStart, secondPatrolEnd);
            }
        }

        objects.Remove(player.transform);
        foreach (KeyCollect levelKey in keys)
        {
            objects.Remove(levelKey.transform);
        }
        if (bonusLife)
        {
            objects.Remove(extraLives[0].transform);
        }

        objects.Remove(exit.transform);
        foreach (EnemyMovement enemy in enemies)
        {
            objects.Remove(enemy.transform);
        }

        positions.Remove(playerPosition);
        positions.Remove(keyPosition);
        if (requiredKeyCount > 1)
        {
            positions.Remove(secondKeyPosition);
        }
        if (bonusLife)
        {
            positions.Remove(extraLifePosition);
        }

        positions.Remove(exitPosition);
        if (!circularPatrol)
        {
            positions.Remove(patrolStart);
            positions.Remove(patrolEnd);
            if (enemies.Length == 2)
            {
                positions.Remove(secondPatrolStart);
                positions.Remove(secondPatrolEnd);
            }
        }
        Shuffle(objects, random);

        foreach (Transform item in objects)
        {
            Vector2Int position = positions[0];
            positions.RemoveAt(0);
            SetPosition(item, position);
        }

        Physics2D.SyncTransforms();
    }

    private static bool ConfigureMazeWalls(GameObject walls, int level)
    {
        Transform maze = walls.transform.Find("MazeWalls");
        if (maze == null && level == 5)
        {
            Transform template = walls.transform.Find("WallTop");
            if (template == null)
            {
                Debug.LogError("[DungeonEscape] Level 5 requires a WallTop object to create its maze walls.");
                return false;
            }

            GameObject mazeObject = new GameObject("MazeWalls");
            mazeObject.transform.SetParent(walls.transform, false);
            maze = mazeObject.transform;

            CreateMazeWall(template, maze, "MazeWestLower", new Vector2(-3f, -3f), new Vector2(0.35f, 3.5f));
            CreateMazeWall(template, maze, "MazeWestUpper", new Vector2(-3f, 2.875f), new Vector2(0.35f, 3.75f));
            CreateMazeWall(template, maze, "MazeCenterLower", new Vector2(0f, -1.875f), new Vector2(0.35f, 5.75f));
            CreateMazeWall(template, maze, "MazeCenterUpper", new Vector2(0f, 3.875f), new Vector2(0.35f, 1.75f));
            CreateMazeWall(template, maze, "MazeEastLower", new Vector2(3f, -3.375f), new Vector2(0.35f, 2.75f));
            CreateMazeWall(template, maze, "MazeEastUpper", new Vector2(3f, 2.375f), new Vector2(0.35f, 4.75f));
            CreateMazeWall(template, maze, "MazeCrossbarWest", new Vector2(-0.5f, -2f), new Vector2(1f, 0.35f));
            CreateMazeWall(template, maze, "MazeCrossbarEast", new Vector2(0.5f, -2f), new Vector2(1f, 0.35f));
        }

        if (maze != null)
        {
            maze.gameObject.SetActive(level == 5);
        }

        return true;
    }

    private static void CreateMazeWall(Transform template, Transform parent, string wallName,
        Vector2 position, Vector2 size)
    {
        GameObject wall = UnityEngine.Object.Instantiate(template.gameObject, parent);
        wall.name = wallName;
        wall.transform.position = new Vector3(position.x, position.y, template.position.z);
        wall.transform.localScale = new Vector3(size.x, size.y, template.localScale.z);
    }

    private static void RandomizeMazePositions(PlayerMovement player, KeyCollect[] keys,
        ExitDoor exit, EnemyMovement[] enemies, ExtraLifePickup[] extraLives)
    {
        CoinCollect[] coins = UnityEngine.Object.FindObjectsByType<CoinCollect>();
        TreasureChestCollect[] chests = UnityEngine.Object.FindObjectsByType<TreasureChestCollect>();
        TimePickup[] timePickups = UnityEngine.Object.FindObjectsByType<TimePickup>();
        Array.Sort(coins, (first, second) => string.CompareOrdinal(first.name, second.name));
        Array.Sort(chests, (first, second) => string.CompareOrdinal(first.name, second.name));
        Array.Sort(timePickups, (first, second) => string.CompareOrdinal(first.name, second.name));

        Vector2Int[] bonusPositions =
        {
            new Vector2Int(-5, 2),
            new Vector2Int(-5, 0),
            new Vector2Int(-5, -2),
            new Vector2Int(-2, -3),
            new Vector2Int(1, -3),
            new Vector2Int(-2, 3),
            new Vector2Int(2, 1),
            new Vector2Int(5, 2)
        };
        int bonusCount = coins.Length + chests.Length + timePickups.Length + extraLives.Length;
        if (keys.Length < 2 || enemies.Length < 2 || extraLives.Length == 0 ||
            bonusCount > bonusPositions.Length)
        {
            Debug.LogError("[DungeonEscape] Level 5 requires two keys, two enemies, a health pickup, and no more than eight bonus objects.");
            return;
        }

        player.SetSpawnPosition(new Vector2(-5f, -4f));
        SetPosition(keys[0].transform, new Vector2Int(-1, 0));
        SetPosition(keys[1].transform, new Vector2Int(2, 3));
        SetPosition(exit.transform, new Vector2Int(5, -4));
        enemies[0].SetPatrolPositions(new Vector2(-4f, -3f), new Vector2(-4f, 4f));
        enemies[1].SetPatrolPositions(new Vector2(4f, 4f), new Vector2(4f, -3f));
        foreach (EnemyMovement enemy in enemies)
        {
            enemy.SetMovementSpeedMultiplier(0.9f);
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.constraints |= RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.linearVelocity = Vector2.zero;
            }
        }

        int bonusIndex = 0;
        PlaceBonusObjects(coins, bonusPositions, ref bonusIndex);
        PlaceBonusObjects(chests, bonusPositions, ref bonusIndex);
        PlaceBonusObjects(timePickups, bonusPositions, ref bonusIndex);
        PlaceBonusObjects(extraLives, bonusPositions, ref bonusIndex, 1);
        Physics2D.SyncTransforms();
    }

    private static void PlaceBonusObjects<T>(T[] objects, Vector2Int[] positions, ref int index,
        int count = int.MaxValue) where T : Component
    {
        int placed = Mathf.Min(objects.Length, count);
        for (int i = 0; i < placed; i++)
        {
            SetPosition(objects[i].transform, positions[index++]);
        }
    }

    private static bool TryChooseObjectivePositions(List<Vector2Int> positions, System.Random random,
        int minX, int maxX, int minY, int maxY, bool needsSecondPatrol,
        bool circularPatrol, Vector2 circleCenter, float circleRadius,
        out Vector2Int player, out Vector2Int key, out Vector2Int exit,
        out Vector2Int patrolStart, out Vector2Int patrolEnd,
        out Vector2Int secondPatrolStart, out Vector2Int secondPatrolEnd)
    {
        player = key = exit = patrolStart = patrolEnd = default;
        secondPatrolStart = secondPatrolEnd = default;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        for (int attempt = 0; attempt < 4000; attempt++)
        {
            Vector2Int candidatePlayer = positions[random.Next(positions.Count)];
            Vector2Int candidateKey = positions[random.Next(positions.Count)];
            Vector2Int candidateExit = positions[random.Next(positions.Count)];

            if (candidatePlayer == candidateKey || candidatePlayer == candidateExit || candidateKey == candidateExit)
            {
                continue;
            }

            if ((candidatePlayer - candidateKey).sqrMagnitude < MinimumPlayerToKeyDistance * MinimumPlayerToKeyDistance ||
                (candidateKey - candidateExit).sqrMagnitude < MinimumKeyToExitDistance * MinimumKeyToExitDistance ||
                (candidatePlayer - candidateExit).sqrMagnitude < MinimumPlayerToExitDistance * MinimumPlayerToExitDistance)
            {
                continue;
            }

            if (circularPatrol &&
                (IsNearCircularPath(candidatePlayer, circleCenter, circleRadius) ||
                 IsNearCircularPath(candidateKey, circleCenter, circleRadius) ||
                 IsNearCircularPath(candidateExit, circleCenter, circleRadius)))
            {
                continue;
            }

            Vector2Int candidateStart = default;
            Vector2Int candidateEnd = default;
            if (!circularPatrol &&
                !TryGetInterceptingPatrol(candidateKey, candidateExit, minX, maxX, minY, maxY,
                    out candidateStart, out candidateEnd))
            {
                continue;
            }

            bool verticalPatrol = candidateStart.x == candidateEnd.x;
            if (!circularPatrol &&
                ((verticalPatrol && candidatePlayer.x == candidateStart.x) ||
                 (!verticalPatrol && candidatePlayer.y == candidateStart.y)))
            {
                continue;
            }

            Vector2Int candidateSecondStart = default;
            Vector2Int candidateSecondEnd = default;
            if (needsSecondPatrol)
            {
                if (!TryGetInterceptingPatrol(candidatePlayer, candidateKey, minX, maxX, minY, maxY,
                        out candidateSecondStart, out candidateSecondEnd) ||
                    candidateStart == candidateSecondStart ||
                    candidateStart == candidateSecondEnd ||
                    candidateEnd == candidateSecondStart ||
                    candidateEnd == candidateSecondEnd)
                {
                    continue;
                }

                bool secondVertical = candidateSecondStart.x == candidateSecondEnd.x;
                if ((secondVertical && candidateExit.x == candidateSecondStart.x) ||
                    (!secondVertical && candidateExit.y == candidateSecondStart.y))
                {
                    continue;
                }
            }

            float score = Vector2Int.Distance(candidatePlayer, candidateKey) +
                          Vector2Int.Distance(candidateKey, candidateExit) +
                          Vector2Int.Distance(candidatePlayer, candidateExit) +
                          (float)random.NextDouble();
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            player = candidatePlayer;
            key = candidateKey;
            exit = candidateExit;
            patrolStart = random.Next(2) == 0 ? candidateStart : candidateEnd;
            patrolEnd = patrolStart == candidateStart ? candidateEnd : candidateStart;
            secondPatrolStart = candidateSecondStart;
            secondPatrolEnd = candidateSecondEnd;
            found = true;
        }

        return found;
    }

    private static bool TryChooseSecondKeyPosition(List<Vector2Int> positions, System.Random random,
        Vector2Int player, Vector2Int firstKey, Vector2Int exit, out Vector2Int secondKey)
    {
        secondKey = default;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        foreach (Vector2Int candidate in positions)
        {
            if (candidate == player || candidate == firstKey || candidate == exit ||
                (candidate - player).sqrMagnitude < MinimumSecondKeyDistance * MinimumSecondKeyDistance ||
                (candidate - firstKey).sqrMagnitude < MinimumSecondKeyDistance * MinimumSecondKeyDistance ||
                (candidate - exit).sqrMagnitude < MinimumSecondKeyDistance * MinimumSecondKeyDistance)
            {
                continue;
            }

            float score = Vector2Int.Distance(candidate, player) +
                          Vector2Int.Distance(candidate, firstKey) +
                          Vector2Int.Distance(candidate, exit) +
                          (float)random.NextDouble();
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            secondKey = candidate;
            found = true;
        }

        return found;
    }

    private static bool TryChooseExtraLifePosition(List<Vector2Int> positions, System.Random random,
        Vector2Int player, Vector2Int firstKey, Vector2Int secondKey, Vector2Int exit,
        out Vector2Int extraLife)
    {
        extraLife = default;
        Vector2 finalRouteMidpoint = new Vector2(
            (secondKey.x + exit.x) * 0.5f,
            (secondKey.y + exit.y) * 0.5f);
        float bestScore = float.NegativeInfinity;
        bool found = false;

        foreach (Vector2Int candidate in positions)
        {
            if ((candidate - player).sqrMagnitude < MinimumExtraLifeObjectiveDistance * MinimumExtraLifeObjectiveDistance ||
                (candidate - firstKey).sqrMagnitude < MinimumExtraLifeObjectiveDistance * MinimumExtraLifeObjectiveDistance ||
                (candidate - secondKey).sqrMagnitude < MinimumExtraLifeObjectiveDistance * MinimumExtraLifeObjectiveDistance ||
                (candidate - exit).sqrMagnitude < MinimumExtraLifeObjectiveDistance * MinimumExtraLifeObjectiveDistance)
            {
                continue;
            }

            float score = -Vector2.Distance(candidate, finalRouteMidpoint) + (float)random.NextDouble() * 0.01f;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            extraLife = candidate;
            found = true;
        }

        return found;
    }

    private static bool IsNearCircularPath(Vector2Int position, Vector2 center, float radius)
    {
        return Mathf.Abs(Vector2.Distance(position, center) - radius) < CircularPathClearance;
    }

    private static void EnsureEnemyCount(int requiredCount)
    {
        EnemyMovement[] enemies = UnityEngine.Object.FindObjectsByType<EnemyMovement>();
        Array.Sort(enemies, (first, second) => string.CompareOrdinal(first.name, second.name));
        if (enemies.Length == 0)
        {
            return;
        }

        for (int i = enemies.Length; i < requiredCount; i++)
        {
            EnemyMovement.CreateAdditionalEnemy(enemies[0], "Enemy" + (i + 1));
        }

        enemies = UnityEngine.Object.FindObjectsByType<EnemyMovement>();
        Array.Sort(enemies, (first, second) => string.CompareOrdinal(first.name, second.name));
        for (int i = requiredCount; i < enemies.Length; i++)
        {
            enemies[i].gameObject.SetActive(false);
        }
    }

    private static void EnsureKeyCount(int requiredCount)
    {
        KeyCollect[] keys = UnityEngine.Object.FindObjectsByType<KeyCollect>();
        Array.Sort(keys, (first, second) => string.CompareOrdinal(first.name, second.name));
        if (keys.Length == 0)
        {
            return;
        }

        for (int i = keys.Length; i < requiredCount; i++)
        {
            KeyCollect.CreateAdditionalKey(keys[0], "Key" + (i + 1));
        }

        keys = UnityEngine.Object.FindObjectsByType<KeyCollect>();
        Array.Sort(keys, (first, second) => string.CompareOrdinal(first.name, second.name));
        for (int i = requiredCount; i < keys.Length; i++)
        {
            keys[i].gameObject.SetActive(false);
        }
    }

    private static void EnsureExtraLifeCount(int requiredCount)
    {
        ExtraLifePickup[] pickups = UnityEngine.Object.FindObjectsByType<ExtraLifePickup>();
        Array.Sort(pickups, (first, second) => string.CompareOrdinal(first.name, second.name));
        if (pickups.Length == 0)
        {
            return;
        }

        for (int i = pickups.Length; i < requiredCount; i++)
        {
            ExtraLifePickup.CreateAdditionalPickup(pickups[0], "ExtraLifePickup" + (i + 1));
        }

        pickups = UnityEngine.Object.FindObjectsByType<ExtraLifePickup>();
        Array.Sort(pickups, (first, second) => string.CompareOrdinal(first.name, second.name));
        for (int i = requiredCount; i < pickups.Length; i++)
        {
            pickups[i].gameObject.SetActive(false);
        }
    }

    private static bool TryGetInterceptingPatrol(Vector2Int key, Vector2Int exit,
        int minX, int maxX, int minY, int maxY,
        out Vector2Int start, out Vector2Int end)
    {
        int deltaX = exit.x - key.x;
        int deltaY = exit.y - key.y;
        if (Mathf.Abs(deltaX) >= Mathf.Abs(deltaY) && Mathf.Abs(deltaX) >= 2)
        {
            int x = (key.x + exit.x) / 2;
            start = new Vector2Int(x, minY);
            end = new Vector2Int(x, maxY);
            return true;
        }

        if (Mathf.Abs(deltaY) >= 2)
        {
            int y = (key.y + exit.y) / 2;
            start = new Vector2Int(minX, y);
            end = new Vector2Int(maxX, y);
            return true;
        }

        start = end = default;
        return false;
    }

    private static void SetPosition(Transform item, Vector2Int position)
    {
        Vector3 worldPosition = item.position;
        worldPosition.x = position.x;
        worldPosition.y = position.y;
        item.position = worldPosition;
    }

    private static void AddTransforms<T>(List<Transform> objects, T[] components) where T : Component
    {
        foreach (T component in components)
        {
            if (!objects.Contains(component.transform))
            {
                objects.Add(component.transform);
            }
        }
    }

    private static void Shuffle<T>(List<T> items, System.Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T item = items[i];
            items[i] = items[swapIndex];
            items[swapIndex] = item;
        }
    }
}
