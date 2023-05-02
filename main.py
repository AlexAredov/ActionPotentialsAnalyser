from utils import *

# Входные данные + резка пд
file = "source/1.txt"
time, voltage = preprocess(file)
action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)

# Графики
plt.figure()
for ap in action_potentials:
    pre_start_index = ap['pre_start']
    end_index = ap['end']
    ap_time = time[pre_start_index:end_index+1]
    ap_voltage = voltage[pre_start_index:end_index+1]

    plt.plot(ap_time, ap_voltage)

plt.xlabel("Time")
plt.ylabel("Voltage")
plt.title("Merged Action Potentials")
plt.show()

for number, ap in enumerate(action_potentials):
    plot_ap(ap, time, voltage, number+1)

plot_phase_4_speeds(action_potentials, time, voltage)
plot_phase_0_speeds(action_potentials, time, voltage)

# !!!очень долго выполняется
plot_radiuses(action_potentials, time, voltage)

# Табличка
destination = f"tables/{file.replace('.txt', '').replace('source/', '')}.xlsx"
save_aps_to_xlsx(destination, action_potentials, time, voltage)