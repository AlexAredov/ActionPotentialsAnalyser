from utils import *

# Получаем данные из большого файлика
file = "source/2.txt"
time, voltage = preprocess(file)

# Резка ПД
action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)

# Сохраняем каждые ПД в папку и получаем временные интервалы
intervals = save_aps_to_txt("saved/2", action_potentials, time, voltage)
print(intervals)