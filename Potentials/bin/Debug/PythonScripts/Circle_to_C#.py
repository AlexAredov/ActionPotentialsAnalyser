import atypic_cardio_analyzer as can
import os
import sys
import shutil
import warnings
import math
from concurrent.futures import ProcessPoolExecutor
import dask.dataframe as dd


# ----------------------------------------------------------------------------------------------
if __name__ == "__main__":
    C_sharp_data = sys.argv[1]
    # C_sharp_data = "D:/programming/projects/py/source/separated_APs/12039.txt"
    warnings.filterwarnings("ignore")

    lines = C_sharp_data.split('\n')

    file_path = lines[0]
    inp_alpha_threshold = float(lines[1].replace(',', '.'))
    inp_start_offset = int(lines[2])
    inp_refractory_period = int(lines[3])
    limit_rad = float(lines[4].replace(',', '.'))

    """
    file_path = C_sharp_data
    inp_alpha_threshold = 2.5
    inp_start_offset = 25
    inp_refractory_period = 10
    limit_rad = 350
    """

    time, voltage = can.open_txt(file_path)
    # time, voltage = preprocess(file_path)

    radius, x, y = can.circle(time, voltage, avr_rad=limit_rad)

    # добавим скорости в 0 и в 4 фазу
    # phase_4_speed, phase_0_speed = 999, 999
    aps = can.find_action_potentials(time, voltage, alpha=inp_alpha_threshold, offset=inp_start_offset,
                                 refractory_period=inp_refractory_period)
    phase_4_speed, phase_0_speed = 999, 999
    for number, ap in enumerate(aps):
        phase_4_speed, phase_0_speed = can.find_voltage_speed(ap, time, voltage)
        if math.isnan(phase_4_speed):
            phase_4_speed = 0
        if math.isnan(phase_0_speed):
            phase_0_speed = 0
        phase_4_speed = round(phase_4_speed, 3)
        phase_0_speed = round(phase_0_speed, 3)

    x_offset = time[ap['start']]
    y_offset = voltage[ap['start']]

    print(round(radius, 3))
    print(round(x, 3))
    print(round(y, 3))
    print(phase_0_speed)
    print(phase_4_speed)
    print(x_offset)
    print(y_offset)

# ----------------------------------------------------------------------------------------------

# Входные данные + резка пд
"""
file = "source/1.txt"
time, voltage = preprocess(file)
action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)

# Графики
plt.figure()
for ap in action_potentials:
    pre_start_index = ap['pre_start']
    end_index = ap['end']
    ap_time = time[pre_start_index:end_index + 1]
    ap_voltage = voltage[pre_start_index:end_index + 1]

    plt.plot(ap_time, ap_voltage)

plt.xlabel("Time")
plt.ylabel("Voltage")
plt.title("Merged Action Potentials")
plt.show()

for number, ap in enumerate(action_potentials):
    plot_ap(ap, time, voltage, number + 1)

plot_phase_4_speeds(action_potentials, time, voltage)
plot_phase_0_speeds(action_potentials, time, voltage)

# !!!очень долго выполняется
plot_radiuses(action_potentials, time, voltage)

# Табличка
destination = f"tables/{file.replace('.txt', '').replace('source/', '')}.xlsx"
save_aps_to_xlsx(destination, action_potentials, time, voltage)
"""
