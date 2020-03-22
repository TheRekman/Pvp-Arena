### EN
Plugins adds functional for creating PvP Arena and map for they. Arena allows dynamic change map by voting or automaticly by map tag, auto teleport to the arena if player die, & some protect if player not in pvp.  Map - massive of tiles what also contain spawn points & tags.
#### Map Commands
`` save <mapname> [tags...]`` - map save in file
`` load <mapname>`` - map load from file;
`` del <mapname>``- delete map file;
`` list [page]`` - map list;
`` tag <tag>`` - map list with tag, start tag with ``!`` return map list without given tag; 
`` addtags (at) <mapname> <tags...>`` - add tags for map, tag can be any;
`` removetag  (rt) <mapname> <tag>`` - remove tag from map;
`` maptags (mt) <mapname>`` - map tag list;
``info <mapname>`` - return map info.
#### Arena Commands
``define <arenaname> <mapname> [align] [width] [height]`` - create arena;
``delete <arenaname>`` - remove arena;
``setmap <arenaname> <mapname>`` - set new map for arena;
``align <arenaname> <align>`` - set new align for arena;
``info <arenaname>`` - return info about arena;
``list [page]`` - return arena list;
``addparam (ap) <arenaname> <param>`` - adds param for arena;
``removeparam (rp) <arenaname> <param>`` - remove param for arena;
``paramlist (pl) [page]`` - return param list;
#### Arena Params
``vote (vt)`` - allow vote for arena, require vote time & repeat vote time in seconds;
``autochange (ac)`` - set random map with given tag in given time in seconds;
``autopvp (ap)`` - activate players pvp if they in arena area;
``autotp (tp)`` - teleport into spawn if player not in pvp;
``autoinvise (ai)`` - give invisibile buff if player not in pvp;
``autospawn (as)`` - teleport into arena spawn if player activate pvp.
**P.S. ** ``vote, autochange`` & ``autopvp, autotp, autoinvise`` can not work at the same time.
#### Vote Commands
To use this command you must be in arena.
``/vote <mapId>`` - vote for map by id in /vote info;
``/vote <mapName>`` - vote or add map by name;
``/vote info`` - get map list and they votes.
#### Permissions
``pvparena.map.use`` - allow use ``/map`` command;
``pvparena.arena.use`` - allow use ``/arena``  command;
``pvparena.vote.use`` - allow use ``/vote``  command;
``pvparena.param.igonre`` - ignore effect arena param.

### RU
Плагин добавляет некоторый функционал для создания ПвП Арен и карт для них. Арена позволяет динамически менять карту путем голосования или автоматически по тегу карты.  Карта - массив тайлов, который также содержит точки спавна и свои теги.
#### Map Commands
`` save <mapname> [tags...]`` - сохранение карты;
`` load <mapname>`` - загрузка карты;
`` del <mapname>``- удаление карты;
`` list [page]`` - список карт;
`` tag <tag>`` - список карт с заданным тегом, указав `!` перед тегом можно вывести карты не имеющие заданный тег; 
`` addtags (at) <mapname> <tags...>`` - добавляет теги для карты, теги могут быть любыми;
`` removetag (rt) <mapname> <tag>`` - удаляет тег для карты;
`` maptags (mt) <mapname>`` - выводит список тегов карты;
``info <mapname>`` - выводит информацию о карте.
#### Arena Commands
``define <arenaname> <mapname> [align] [width] [height]`` - создание арены;
``delete <arenaname>`` - удаление арены;
``setmap <arenaname> <mapname>`` - установление новой карты для заданной арены;
``align <arenaname> <align>`` - установление нового выравния карты для заданной арены;
``info <arenaname>`` - выводит информацию об арене
``list [page]`` - возвращает список арен;
``addparam (ap) <arenaname> <param>`` - добавляет параметр арены;
``removeparam (rp) <arenaname> <param>`` - удаляет параметр арены;
``paramlist (pl) [page]`` - возвращает список параметров;
#### Arena Params
``vote (vt)`` - позволяет голосовать за карту арены, требует время голосования и время повтора голосования в секундах;
``autochange (ac)`` - устанавливает случайную карту с заданным тегом через заданный промежуток времени в секундах;
``autopvp (ap)`` - активирует ПвП игрока на арене;
``autotp (tp)`` - телепортирует на спавн если игрок не в пвп;
``autoinvise (ai)`` -  выдает бафф невидимости если игрок не в пвп;
``autospawn (as)`` - телепортирует на спавн арены если игрок активировал пвп.
**P.S. ** ``vote, autochange`` и ``autopvp, autotp, autoinvise`` не могут работать в одно и тоже время.
#### Vote Commands
Для использования этой команды вы должны быть на арене.
``/vote <mapId>`` - голосование за карту по ID в голосовании;
``/vote <mapName>`` - голосование за карту по имени;
``/vote info`` - выводит список карт и их голоса.
#### Permissions
``pvparena.map.use`` - позволяет использовать комманду ``/map``;
``pvparena.arena.use`` - позволяет использовать комманду ``/arena``;
``pvparena.vote.use`` - позволяет использовать комманду ``/vote`` ;
``pvparena.param.igonre`` - игнорирование эффектов параметры арены.