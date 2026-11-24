# Шпаргалка по git (для этого проекта)

Проект: `E:\858\BabbdiWork\Project`. Все команды — в этом каталоге (в PowerShell: `cd 'E:\858\BabbdiWork\Project'`).

## Каждый день — три команды

```
git status               # что изменилось с прошлого снимка
git add -A               # взять все изменения
git commit -m "что сделал"   # сделать снимок
```

Примеры сообщений: `"duel cabinets: fronts face the room"`, `"snake: one turn per step"`, `"fix: crate too tall"`.

## Посмотреть историю

```
git log --oneline            # список снимков, сверху новые
git show <хеш>               # что именно поменялось в этом снимке
git diff                     # что поменялось сейчас, но ещё не снято
git diff v0.10.43-baseline -- BabbdiCoop/Net.cs   # сравнить файл с прошлым тегом
```

## Откатиться

```
git checkout -- BabbdiCoop/SomeFile.cs    # вернуть один файл как было
git restore --source=v0.10.43-split .     # вернуть весь проект на тег
git reset --hard <хеш>                    # жёстко откатить всё (осторожно)
```

## Бирки версий

```
git tag v0.10.44                # пометить текущий снимок
git tag -l                      # список бирок
git push --tags                 # отправить бирки на GitHub
```

Правило: собралось и работает — коммит. Отдал другу — тег `vX.Y.Z`.

## GitHub (когда будет аккаунт)

Без командной строки: **GitHub Desktop** → Add local repository → выбрать папку проекта → Publish repository.
Приватный репозиторий — бесплатно, 250 МБ проект влезает.

Через консоль:

```
git remote add origin https://github.com/ТВОЙ_НИК/babbdi-coop.git
git push -u origin master
git push --tags
```

Пароль не подойдёт — нужен **Personal Access Token** (Settings → Developer settings → Tokens) или вход через GitHub Desktop / `gh auth login`.

**Перед первым пушем** поставь свой email, иначе коммиты не привяжутся к профилю:

```
git config user.email "твой-github-email@example.com"
git config user.name "Твой Ник"
```

## Полезное

```
git stash            # временно убрать правки, чтобы проверить «как было»
git stash pop        # вернуть их
git branch experiment   # отдельная копия мира для эксперимента
git checkout experiment
git checkout master     # вернуться
```

## Чего не делать

- Не коммитить `bin/`, `obj/` — они уже в `.gitignore`.
- Не хранить в репозитории личные данные и ключи.
- Не пушить `mi26.glb` и прочие большие файлы в **другой** репозиторий с лимитом 100 МБ на файл — здесь самый большой 8,75 МБ, всё нормально.
