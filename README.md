# Quest Soft Player (QSP) language provider

## Зочем?
— А-а, зачем мне эта ерунда, если уже есть [QGen](http://qsp.su/index.php?option=com_content&task=view&id=46&Itemid=56)?! Обчитаются своих книжечек, а потом в гаражах кодють! Такую страну про...

Согласен, если вы — гений программирования, никогда не ошибаетесь в синтаксисе и не забываете, что делают операторы/функции, всегда даете правильные названия переменным и локациями и мгновенно можете отыскать в коде всё, что душе угодно, то что вы здесь забыли? Итак:

## Щито оно умеет
* На лету проверять синтаксис и невменяемо орать на вас в случае ошибок

    ![](Images/syntaxChecker.gif?raw=true)

    — Вопль тысячи токенов
* Подсказывать, что делает та или иная функция

    ![hover](Images/hover.gif?raw=true "Hover")
* Переименовывать переменные и локации (F2)

    ![](Images/rename.gif?raw=true)
* Искать определения переменных/локации и их использование (F12)

    ![](Images/definitions.gif?raw=true)
* Форматировать убогий код

    ![](Images/format.gif?raw=true)
* Дико тормозить на больших файлах... к сожалению.

## Щито оно **не** умеет, но могло бы
* IntelliSense, aka умные подсказки во время набора.
* Работать с проектом. К сожалению, сейчас оно работает с каждым исходником отдельно, и те друг о друге ничего не знают, хотя счастье так близко. Такая вот драма.

## Допустим, я хочу попробовать
Под Windows нужен .Net Framework >= 4.6.1 (скачать можно [тут](https://dotnet.microsoft.com/download/dotnet-framework), если лень гуглить), а под другие платформы [LSP](https://github.com/gretmn102/FParserQSP) пока не компилируется. Через Mono как-то можно, но лень. Да и чем компилировать исходники QSP'а, если известный [компилятор](http://qsp.su/index.php?option=com_content&task=view&id=52&Itemid=56) ([его исходники](https://github.com/QSPFoundation/qsp/tree/master/txt2gam)) — под Windows?

### Установка
Проста до безобразия:
1. Установите [VS Code](https://code.visualstudio.com/Download)
2. Создайте новый файл с Hello World'ом:
    ```qsp
    # начало
    'На первый день Byte создал QSP...'
    -
    ```
3. Непременно сохраните его с расширением `.qsps` — отныне это формат для всех исходников QSP'а, просто потому что
4. И VS Code любезно должен предложить поискать расширение. Смело давите "Search"
5. Вот то единственное, что найдется (если, конечно, у меня не появились соперники, грр), и устанавливайте
4. Всё

### Ну накодил я, что теперь-то?
Скомпилировать в `.qsp`. Сделать это очень просто: жмете `F1`, введите `QSP: Build`, и по-идеи, должно скомпилироваться в то же место, что и ваш файл с исходником.

**Важно**: исходник должен быть в кодировке Cyrillic (Windows 1251), иначе, например, классический плеер станет отображать кракозябры, вместо нормального текста. Как сменить кодировку, сами найдете, дети интернетов.

По-хорошему, я мог бы внедрить Watcher, чтобы он компилировал исходник каждый раз, когда его сохраняют, но обойдетесь.

### У меня уже есть игра, но скомпилирована в `.qsp`, как ее декомпилировать?
Запускаете (ох и ах) QGen. Жмете `Игра` -> `Экспорт` -> `Текстовый файл формата TXT2QSP...` и, собственно, сохраняете, куда душа пожелает. Не забудьте сменить расширение файла на `.qsps`.

Кстати, существует отдельная программа [qsp2txt](http://qsp.su/index.php?option=com_agora&task=topic&id=1180&p=1&Itemid=57#p26046), но там же сообщением ниже пишут:
> Эта утилита экспортит не все локации. Если в игре много локаций, она бесполезна.

Так что использовать на свой страх и риск.

По-хорошему, нужно бы выдрать из исходников QGen'а этот функционал, но вы уже знаете ответ :wink:

## Важно
Поскольку добровольцем на испытания вызвался лишь один человек, то черт его знает, заработает ли оно вообще у вас. У меня-то, как у автора, всегда всё работает, ха-ха! Но делаем для других с любовью, так что, пожалуйста, все ошибки, недомогания и вопросы шлите на [форум](http://qsp.su/index.php?option=com_agora&task=topic&id=1286&Itemid=57) или [сюда](https://github.com/gretmn102/QSP-VSCode/issues) или свяжитесь со мной по нику Pseudopod#4902 в Discord'е.

## Благодарности
* Garrett Fletcher, спасибо, что помог разобраться в этом языке. Без тебя я бы вряд ли справился. Да и приятно было пообщаться с родной душой и обменяться идеями (чего только один QSP на Lisp'е стоит!)
* Бари, спасибо, что отвлекал своими философствованиями, но без этого я бы перегорел
* Ionide-Fsharp, которыми вдохновился и у них же беспардонно стащил реализацию JSON-RPC, да и вообще всё стащил, чего уж греха таить. Но клятвенно поклялся, что сделаю по-своему!

## Как это скомпилировать?
— Я не доверяю тебе, потому сейчас пересмотрю твой код...

*Снимает шляпу: чтобы копаться в **этом** нужно быть действительно не из робкого десятка*.

— ...скомпилирую его, а потом запущу, так уж и быть, дурак...

— Ладно, нужен dotnet 3.1.200, а дальше запустить \**невнятное бормотание*\*...

## Будущее проекта
Его нет: всё — тлен... пока что.
