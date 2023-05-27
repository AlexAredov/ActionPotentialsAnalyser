TODO: Написать инструкцию заново, текущая неактуальна

Пробный датасет находится в папке Potentials/sourse

# Инструкция по запуску:  
1. Перед запуском нужно установить python 3.9.x по пути C:\Python39\python.exe
2. Установить библиотеки через терминал cmd Win + R: 

pip install numpy, matplotlib, pandas, scipy, skimage, itertools, openpyxl
(вроде все, но если будет на что-то еще ругаться, тоже ставим)

3. Установить библиотеки в VS 2022 через NuGet (или еще как-нибудь, главное установить)
см. картинки в чате, лень дублировать

4. Проверить поддержку .NET Framework 4.7.2
5. Молиться, чтобы оно запустилось

# Инструкция по использованию программы для анализа потенциалов действий
## 1. Введение

Эта программа создана для анализа потенциалов действий, в частности атипичных кардиомиоцитов синусного узла. Она обрабатывает текстовые файлы (.txt), содержащие временной ряд напряжений, и вычисляет различные характеристики потенциалов действий, включая скорости в фазах 0 и 4 и радиус кривизны. Результаты могут быть сохранены в таблицу Excel для дальнейшего анализа.

## 2. Установка и требования

Установка программы осуществляется с помощью инсталлятора. Программа требует современного оборудования и достаточно быстрого носителя для эффективной работы, поскольку объем обрабатываемых данных может быть значительным. Обработка данных может занять несколько минут, в зависимости от размера входных данных. Текущая скорость обработки составляет приблизительно 1 минута реального времени на 1 секунду записи на современном оборудовании.

## 3. Интерфейс пользователя
### Основные элементы интерфейса:
- "Open file": Открывает файл с записью потенциалов действий.
- "Python": Если необходимый для работы программы python.exe не обнаружен, с помощью этой кнопки вы можете указать его местоположение.
- "Clear": Очищает все данные. Используйте эту функцию, если программа начинает работать некорректно.
- "Save": Сохраняет данные в таблицу Excel.

### Поля для ввода данных:
- "Find AP by time": Поиск конкретного потенциала действия по времени.
- "Find AP by number": Поиск конкретного потенциала действия по номеру.
- "Alpha": Значение скорости напряжения, которое будет соответствовать началу фазы 0 потенциала действия. Необходимо для корректного разделения потенциалов действия между собой.
- "Offset": Значение, которое "двигает" точку начала фазы 0.
- "Refractory period": Значение, которое означает, сколько миллисекунд данное значение напряжения будет считаться конкретным потенциалом действия.
- "Limit radius": При радиусе больше данного значения будет происходить перерасчет радиуса.
- "Window size": Количество точек, которые будут браться для расчета доверительного интервала для графиков скоростей и радиуса.

### Графики:
- Скорости: Значения скорости определенного потенциала действия в определенной фазе.
- Радиусы: Значения радиусов определенных потенциалов действия.
- Все потенциалы: Исходные данные, зависимость напряжения от времени.

## 4. Обработка ошибок и решение проблем
TODO

## 5. Контактная информация и поддержка
TODO

# Рекомендации по использованию:
1. Добавить последовательность интерфейса
То есть залочить кнопочки которые до какого-то момента нельзя тыкать, как СС любит

Логика работы кнопок:
Open file -> PlotAll, Save 
PlotAll -> FindAP
FindAP -> "<" ">" 

2. Добавить другие проверки на додика, которые могут все крашнуть
Я что-то добавлял, что-то мог пропустить

3. mm:ss:ff означает Минуты, секунды, миллисекунды
