from utils import *

# Получаем данные из большого файла и предобрабатываем
file = "source/1.txt"
time, voltage = preprocess(file)

# Резка ПД
action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)


# График найденных ПД (вместе и отдельно)
plot_aps(action_potentials, time, voltage)

for ap in action_potentials:
    plot_ap(ap, time, voltage)

# Графики для скоростей в 4 и 0 фазах
plot_phase_4_speeds(action_potentials, time, voltage)
plot_phase_0_speeds(action_potentials, time, voltage)

# График для радиусов
plot_radiuses(action_potentials, time, voltage)