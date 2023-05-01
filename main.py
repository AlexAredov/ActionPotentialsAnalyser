import numpy as np
import matplotlib.pyplot as plt
from utils import preprocess, find_action_potentials, find_voltage_speed

time, voltage = preprocess("1.txt")
action_potentials = find_action_potentials(time, voltage)

#Все ПД
plt.figure()
plt.plot(time, voltage)
plt.xlabel("Время (мс)")
plt.ylabel("Напряжение (мВ)")
plt.title("Исходный сигнал")
plt.show()

plt.figure()
for i, ap in enumerate(action_potentials):
    pre_start_index = ap['pre_start']
    end_index = ap['end']
    ap_time = time[pre_start_index:end_index+1]
    ap_voltage = voltage[pre_start_index:end_index+1]

    plt.plot(ap_time, ap_voltage)
    plt.scatter(time[ap['start']], voltage[ap['start']], marker='o', color='red', label='Начало ПД')

    plt.xlabel("Время (мс)")
    plt.ylabel("Напряжение (мВ)")
    plt.title(f"{i+1}-й ПД")

    plt.show()

# Скорости
phase_4_speed, phase_0_speed = find_voltage_speed(action_potentials[0], time, voltage)
print(f"Скорость в фазе 4 = {phase_4_speed:.2f}")
print(f"Скорость в фазе 0 = {phase_0_speed:.2f}")