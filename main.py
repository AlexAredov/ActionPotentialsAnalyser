from utils import *

# Входные данные + резка пд
file = "1.txt"
time, voltage = preprocess(file)
action_potentials = find_action_potentials(time, voltage)

# Графики
plt.figure()
plt.plot(time, voltage)
plt.xlabel("Время (мс)")
plt.ylabel("Напряжение (мВ)")
plt.title("Исходный сигнал")
plt.show()

# for ap in action_potentials:
#     plot_ap(ap, time, voltage)
plot_phase_4_speed(action_potentials, time, voltage)
plot_phase_0_speed(action_potentials, time, voltage)

# Табличка
save_aps_to_xlsx(f"tables/{file.replace('.txt', '')}.xlsx", action_potentials, time, voltage)