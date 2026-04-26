SELECT DISTINCT CONCAT_WS(',', ua."DuelId"::text, ua."UserId"::text, ua."TaskKey"::text) AS action_key
FROM "UserActions" ua
ORDER BY ua."DuelId", ua."UserId", ua."TaskKey";