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
    warnings.filterwarnings("ignore")

    lines = C_sharp_data.split('\n')

    file_path = lines[0]
    inp_alpha_threshold = float(lines[1].replace(',', '.'))
    inp_start_offset = int(lines[2])
    inp_refractory_period = int(lines[3])
    limit_rad = float(lines[4].replace(',', '.'))

    time, voltage = can.open_txt(file_path)
    radius, x, y = can.circle(time, voltage, avr_rad=limit_rad)
    aps = can.find_action_potentials(time, voltage, alpha=inp_alpha_threshold, offset=inp_start_offset,
                                 refractory_period=inp_refractory_period)

    phase_4_speed, phase_0_speed = 999, 999
    for number, ap in enumerate(aps):
        phase_4_speed, phase_0_speed = can.find_voltage_speed(ap, time, voltage)
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