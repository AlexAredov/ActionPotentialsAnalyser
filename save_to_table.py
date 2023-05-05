from utils import *

# Получаем данные из большого файла и предобрабатываем
file = "source/1.txt"
time, voltage = preprocess(file)

# Резка ПД
action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)

# Сохранение в табличку
destination = f"tables/{file.replace('.txt', '').replace('source/', '')}.xlsx"
save_aps_to_xlsx(destination, action_potentials, time, voltage)